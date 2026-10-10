import { openDB, DBSchema, IDBPDatabase } from 'idb';

export type CaptureQueueStatus = 'queued' | 'uploading' | 'received' | 'needs_retake' | 'failed';

export interface CaptureQueueItem {
  id: string; // Client GUID idempotency key
  category: 'diesel' | 'gas' | 'electricity' | 'shipment';
  blob?: Blob;
  fileName?: string;
  fileSizeBytes?: number;
  manualQuantity?: number;
  manualSlipNumber?: string;
  manualUnit?: string;
  status: CaptureQueueStatus;
  retryCount: number;
  receiptCode?: string;
  error?: string;
  createdAt: string;
  syncedAt?: string;
}

interface CarbonBillCaptureDB extends DBSchema {
  captureQueue: {
    key: string;
    value: CaptureQueueItem;
    indexes: {
      'by-status': CaptureQueueStatus;
      'by-created': string;
    };
  };
}

let dbPromise: Promise<IDBPDatabase<CarbonBillCaptureDB>> | null = null;

function getDb(): Promise<IDBPDatabase<CarbonBillCaptureDB>> {
  if (!dbPromise) {
    dbPromise = openDB<CarbonBillCaptureDB>('carbonbill-capture-db', 2, {
      upgrade(db, oldVersion) {
        if (oldVersion < 2) {
          if (db.objectStoreNames.contains('captureQueue')) {
            db.deleteObjectStore('captureQueue');
          }
          const store = db.createObjectStore('captureQueue', { keyPath: 'id' });
          store.createIndex('by-status', 'status');
          store.createIndex('by-created', 'createdAt');
        }
      },
    });
  }
  return dbPromise;
}

export async function enqueueCapture(
  item: Omit<CaptureQueueItem, 'status' | 'retryCount' | 'createdAt'>
): Promise<CaptureQueueItem> {
  const db = await getDb();
  const queueItem: CaptureQueueItem = {
    ...item,
    status: 'queued',
    retryCount: 0,
    createdAt: new Date().toISOString(),
  };

  await db.put('captureQueue', queueItem);
  triggerBackgroundSync();
  return queueItem;
}

export async function getAllCaptureItems(): Promise<CaptureQueueItem[]> {
  const db = await getDb();
  const all = await db.getAll('captureQueue');
  return all.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
}

export async function getPendingCaptures(): Promise<CaptureQueueItem[]> {
  const db = await getDb();
  const index = db.transaction('captureQueue').store.index('by-status');
  const queued = await index.getAll('queued');
  const failed = await index.getAll('failed');
  return [...queued, ...failed];
}

export async function updateCaptureStatus(
  id: string,
  status: CaptureQueueStatus,
  receiptCode?: string,
  error?: string
): Promise<void> {
  const db = await getDb();
  const item = await db.get('captureQueue', id);
  if (!item) return;

  item.status = status;
  if (receiptCode) item.receiptCode = receiptCode;
  if (error) item.error = error;
  if (status === 'received') item.syncedAt = new Date().toISOString();
  if (status === 'failed') item.retryCount++;

  await db.put('captureQueue', item);
}

/**
 * Executes upload sync for all queued items
 */
export async function processQueueUploads(
  apiBaseUrl: string = '/api/v1/documents',
  token?: string
): Promise<{ succeeded: number; failed: number }> {
  const pending = await getPendingCaptures();
  let succeeded = 0;
  let failed = 0;

  const effectiveToken = token || (typeof window !== 'undefined' ? localStorage.getItem('carbonbill_token') : null);

  for (const item of pending) {
    await updateCaptureStatus(item.id, 'uploading');

    try {
      const headers: Record<string, string> = {
        'Idempotency-Key': item.id,
      };
      if (effectiveToken) {
        headers['Authorization'] = `Bearer ${effectiveToken}`;
      }

      let response: Response;

      if (item.blob) {
        const formData = new FormData();
        formData.append('file', item.blob, item.fileName || 'capture.webp');
        formData.append('source', 'phone');
        formData.append('category', item.category);
        formData.append('docType', mapCategoryToDocType(item.category));

        response = await fetch(apiBaseUrl, {
          method: 'POST',
          headers,
          body: formData,
        });
      } else if (item.manualQuantity) {
        headers['Content-Type'] = 'application/json';
        const manualPayload = {
          assetId: null,
          docType: mapCategoryToDocType(item.category),
          slipNumber: item.manualSlipNumber || `MANUAL-${item.id.slice(0, 6)}`,
          period: new Date(item.createdAt).toISOString().slice(0, 7),
          quantity: item.manualQuantity,
          unit: item.manualUnit || 'litre',
        };

        response = await fetch(`${apiBaseUrl}/manual-entry`, {
          method: 'POST',
          headers,
          body: JSON.stringify(manualPayload),
        });
      } else {
        await updateCaptureStatus(item.id, 'failed', undefined, 'Missing blob or manual data');
        failed++;
        continue;
      }

      if (response.ok || response.status === 202 || response.status === 200) {
        const data = await response.json().catch(() => ({}));
        const receipt = (data.id || item.id).slice(0, 8).toUpperCase();
        await updateCaptureStatus(item.id, 'received', receipt);
        succeeded++;
      } else {
        const errText = await response.text().catch(() => 'Upload failed');
        await updateCaptureStatus(item.id, 'failed', undefined, errText);
        failed++;
      }
    } catch (err: any) {
      await updateCaptureStatus(item.id, 'failed', undefined, err?.message || 'Network error');
      failed++;
    }
  }

  return { succeeded, failed };
}

function mapCategoryToDocType(category: string): string {
  switch (category) {
    case 'diesel':
      return 'DieselSlip';
    case 'gas':
      return 'GasBill';
    case 'electricity':
      return 'ElectricityBill';
    case 'shipment':
      return 'ShippingChallan';
    default:
      return 'GeneralDocument';
  }
}

export function triggerBackgroundSync(): void {
  if (typeof window !== 'undefined' && 'serviceWorker' in navigator && 'SyncManager' in window) {
    navigator.serviceWorker.ready
      .then((reg: any) => {
        return reg.sync.register('carbonbill-capture-sync');
      })
      .catch(() => {
        // Fallback: window online listener handles sync
      });
  }
}

export async function retryCaptureItem(id: string): Promise<void> {
  const db = await getDb();
  const item = await db.get('captureQueue', id);
  if (!item) return;

  item.status = 'queued';
  item.error = undefined;
  await db.put('captureQueue', item);
  await processQueueUploads();
}

// Auto-register window online listener
if (typeof window !== 'undefined') {
  window.addEventListener('online', () => {
    processQueueUploads();
  });
}
