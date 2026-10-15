import React, { useState, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Fuel,
  Flame,
  Zap,
  Truck,
  Camera,
  Upload,
  CheckCircle2,
  AlertCircle,
  RefreshCw,
  Clock,
  Layers,
  FileText,
  Bell,
  Wifi,
  WifiOff,
  ArrowLeft,
  ExternalLink,
  MoreHorizontal,
  X,
  RotateCcw,
  Trash2,
} from 'lucide-react';
import {
  enqueueCapture,
  getAllCaptureItems,
  processQueueUploads,
  retryCaptureItem,
  deleteCaptureItem,
  CaptureQueueItem,
} from './offlineCaptureQueue';
import { processAndCompressImage } from './imageProcessing';
import { requestAndSubscribePush } from './pushSubscription';
import { formatBanglaNumber, formatBanglaDate } from '../../shared/formatters';
import { apiFetch } from '../../lib/apiClient';

type CaptureCategory = 'diesel' | 'gas' | 'electricity' | 'shipment' | 'other';

function formatLocalizedNumber(num: number | string, isBangla: boolean): string {
  if (isBangla) return formatBanglaNumber(num);
  const n = typeof num === 'string' ? parseFloat(num) : num;
  return isNaN(n) ? String(num) : n.toLocaleString('en-US');
}

function formatLocalizedDate(dateInput: Date | string, isBangla: boolean): string {
  if (isBangla) return formatBanglaDate(dateInput);
  const d = typeof dateInput === 'string' ? new Date(dateInput) : dateInput;
  if (isNaN(d.getTime())) return '';
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
}

function detectCategoryFromFileName(fileName: string): CaptureCategory {
  const lower = fileName.toLowerCase();
  if (lower.includes('gas') || lower.includes('titas') || lower.includes('bakhrabad') || lower.includes('jalalabad')) return 'gas';
  if (lower.includes('diesel') || lower.includes('fuel') || lower.includes('octane') || lower.includes('petrol') || lower.includes('pocl')) return 'diesel';
  if (lower.includes('ship') || lower.includes('truck') || lower.includes('transport') || lower.includes('challan') || lower.includes('freight')) return 'shipment';
  if (lower.includes('elec') || lower.includes('power') || lower.includes('desco') || lower.includes('dpdc') || lower.includes('reb') || lower.includes('bill')) return 'electricity';
  return 'other';
}

export function CaptureScreen({
  onBack,
  onGoToReview,
}: {
  onBack?: () => void;
  onGoToReview?: (docId: string) => void;
} = {}) {
  const { t, i18n } = useTranslation();
  const isBangla = i18n.language === 'bn';

  const [selectedCategory, setSelectedCategory] = useState<CaptureCategory | null>(null);
  const [isBatchMode, setIsBatchMode] = useState<boolean>(false);
  const [batchCount, setBatchCount] = useState<number>(0);
  const [isManualMode, setIsManualMode] = useState<boolean>(false);
  const [manualQuantity, setManualQuantity] = useState<string>('');
  const [manualSlipNumber, setManualSlipNumber] = useState<string>('');
  const [manualUnit, setManualUnit] = useState<string>('litre');
  const [confirmedReceipt, setConfirmedReceipt] = useState<{ id: string; code: string; serverDocId?: string } | null>(null);
  const [submissions, setSubmissions] = useState<CaptureQueueItem[]>([]);
  const [isProcessing, setIsProcessing] = useState<boolean>(false);
  const [captureError, setCaptureError] = useState<string | null>(null);
  const [isOnline, setIsOnline] = useState<boolean>(typeof navigator !== 'undefined' ? navigator.onLine : true);
  const [pushSubscribed, setPushSubscribed] = useState<boolean>(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const [isDragging, setIsDragging] = useState<boolean>(false);

  // Live Camera Viewfinder State (for PC webcam & mobile browser camera)
  const [isCameraOpen, setIsCameraOpen] = useState<boolean>(false);
  const [cameraStream, setCameraStream] = useState<MediaStream | null>(null);
  const [cameraError, setCameraError] = useState<string | null>(null);
  const [capturedPhotoUrl, setCapturedPhotoUrl] = useState<string | null>(null);
  const [capturedBlob, setCapturedBlob] = useState<Blob | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const cameraInputRef = useRef<HTMLInputElement>(null);
  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);

  const categories = [
    {
      key: 'diesel' as const,
      labelBn: 'ডিজেল স্লিপ',
      labelEn: 'Diesel Slip',
      subBn: 'জেনারেটর ও বয়লার ফুয়েল',
      subEn: 'Generator & Boiler Fuel',
      icon: Fuel,
      color: 'bg-amber-500 text-white',
      accentBg: 'bg-amber-50',
      activeBorder: 'border-amber-500 ring-2 ring-amber-400',
      badgeColor: 'bg-amber-100 text-amber-900',
      defaultUnitBn: 'লিটার',
      defaultUnitEn: 'Litres',
    },
    {
      key: 'gas' as const,
      labelBn: 'গ্যাস বিল',
      labelEn: 'Gas Bill',
      subBn: 'তিতাস / বাখরাবাদ আরএমএস',
      subEn: 'Titas / Bakhrabad RMS',
      icon: Flame,
      color: 'bg-orange-500 text-white',
      accentBg: 'bg-orange-50',
      activeBorder: 'border-orange-500 ring-2 ring-orange-400',
      badgeColor: 'bg-orange-100 text-orange-900',
      defaultUnitBn: 'ঘনমিটার',
      defaultUnitEn: 'm³',
    },
    {
      key: 'electricity' as const,
      labelBn: 'বিদ্যুৎ বিল',
      labelEn: 'Electricity Bill',
      subBn: 'ডেসকো / ডিপিডিসি / আরইবি',
      subEn: 'DESCO / DPDC / REB',
      icon: Zap,
      color: 'bg-yellow-500 text-white',
      accentBg: 'bg-yellow-50',
      activeBorder: 'border-yellow-500 ring-2 ring-yellow-400',
      badgeColor: 'bg-yellow-100 text-yellow-900',
      defaultUnitBn: 'কেডব্লিউএইচ',
      defaultUnitEn: 'kWh',
    },
    {
      key: 'shipment' as const,
      labelBn: 'পরিবহন চালান',
      labelEn: 'Transport Challan',
      subBn: 'ট্রাক ও কার্গো চালান',
      subEn: 'Truck & Cargo Challan',
      icon: Truck,
      color: 'bg-blue-500 text-white',
      accentBg: 'bg-blue-50',
      activeBorder: 'border-blue-500 ring-2 ring-blue-400',
      badgeColor: 'bg-blue-100 text-blue-900',
      defaultUnitBn: 'কেজি',
      defaultUnitEn: 'kg',
    },
    {
      key: 'other' as const,
      labelBn: 'অন্যান্য',
      labelEn: 'Other',
      subBn: 'বিবিধ রশিদ ও খরচ',
      subEn: 'Misc Slips & Expenses',
      icon: MoreHorizontal,
      color: 'bg-purple-600 text-white',
      accentBg: 'bg-purple-50',
      activeBorder: 'border-purple-600 ring-2 ring-purple-400',
      badgeColor: 'bg-purple-100 text-purple-900',
      defaultUnitBn: 'ইউনিট',
      defaultUnitEn: 'units',
    },
  ];

  useEffect(() => {
    if (selectedCategory) {
      const found = categories.find((c) => c.key === selectedCategory);
      if (found) {
        setManualUnit(isBangla ? found.defaultUnitBn : found.defaultUnitEn);
      }
    } else {
      setManualUnit(isBangla ? 'লিটার' : 'Litres');
    }
  }, [isBangla, selectedCategory]);

  useEffect(() => {
    loadQueue();

    const handleOnline = () => {
      setIsOnline(true);
      triggerSync();
    };
    const handleOffline = () => setIsOnline(false);

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
      stopCamera();
    };
  }, []);

  // Connect video stream whenever cameraStream changes and videoRef is rendered
  useEffect(() => {
    if (isCameraOpen && cameraStream && videoRef.current) {
      videoRef.current.srcObject = cameraStream;
      videoRef.current.play().catch((err) => console.warn('Video play error:', err));
    }
  }, [isCameraOpen, cameraStream]);

  async function loadQueue() {
    const localList = await getAllCaptureItems();
    try {
      const serverDocs = await apiFetch<
        Array<{
          id: string;
          status: string;
          fileName: string;
          contentType: string;
          fileSizeBytes: number;
          capturedAtUtc: string;
          received: boolean;
          isDuplicate: boolean;
        }>
      >('/api/v1/documents/my-submissions?limit=30');

      if (serverDocs && Array.isArray(serverDocs) && serverDocs.length > 0) {
        // Map server docs to CaptureQueueItem structure
        const serverItems: CaptureQueueItem[] = serverDocs.map((doc) => {
          let mappedStatus: CaptureQueueItem['status'] = 'received';
          if (doc.status === 'Calculated' || doc.status === 'Confirmed') {
            mappedStatus = 'calculated';
          } else if (doc.status === 'NeedsReview' || doc.status === 'Uploaded' || doc.status === 'Extracting') {
            mappedStatus = 'needs_review';
          } else if (doc.status === 'Duplicate') {
            mappedStatus = 'duplicate';
          } else if (doc.status === 'Failed') {
            mappedStatus = 'failed';
          }

          const cat = detectCategoryFromFileName(doc.fileName || '');
          return {
            id: doc.id,
            category: cat,
            fileName: doc.fileName,
            fileSizeBytes: doc.fileSizeBytes,
            status: mappedStatus,
            retryCount: 0,
            receiptCode: doc.id.slice(0, 8).toUpperCase(),
            serverDocId: doc.id,
            createdAt: doc.capturedAtUtc,
          };
        });

        // Filter local items that are not yet on the server (e.g. pending queued/uploading)
        const serverIds = new Set(serverDocs.map((s) => s.id.toLowerCase()));
        const unsyncedLocals = localList.filter((loc) => {
          const locServerId = loc.serverDocId ? loc.serverDocId.toLowerCase() : '';
          const locId = loc.id.toLowerCase();
          return !serverIds.has(locServerId) && !serverIds.has(locId);
        });

        setSubmissions([...unsyncedLocals, ...serverItems]);
        return;
      }
    } catch (e) {
      console.warn('Could not fetch server submissions, using local queue:', e);
    }
    setSubmissions(localList);
  }

  async function triggerSync() {
    setIsProcessing(true);
    try {
      await processQueueUploads();
      await loadQueue();
    } finally {
      setIsProcessing(false);
    }
  }

  async function handleRetry(id: string) {
    setIsProcessing(true);
    try {
      await retryCaptureItem(id);
      await loadQueue();
    } finally {
      setIsProcessing(false);
    }
  }

  async function handleRemoveSubmission(sub: CaptureQueueItem) {
    const confirmMsg = isBangla
      ? 'আপনি কি নিশ্চিত যে এই সাবমিশনটি তালিকা থেকে মুছে ফেলতে চান? এটি স্থায়ীভাবে মুছে ফেলা হবে।'
      : 'Are you sure you want to remove this submission? This will permanently delete it.';
    if (!window.confirm(confirmMsg)) return;

    setDeletingId(sub.id);
    try {
      // 1. Delete from server if the document exists on backend
      const docIdToDelete = sub.serverDocId || sub.id;
      if (docIdToDelete) {
        try {
          await apiFetch(`/api/v1/documents/${docIdToDelete}`, {
            method: 'DELETE',
          });
        } catch (serverErr) {
          console.warn('Backend document deletion note:', serverErr);
        }
      }

      // 2. Delete from offline IndexedDB queue
      await deleteCaptureItem(sub.id);
      if (sub.serverDocId && sub.serverDocId !== sub.id) {
        await deleteCaptureItem(sub.serverDocId);
      }

      // 3. Reset confirmation badge if this document is currently displayed
      if (confirmedReceipt?.id === sub.id || confirmedReceipt?.serverDocId === sub.id) {
        setConfirmedReceipt(null);
      }

      // 4. Reload queue items
      await loadQueue();
    } catch (err: any) {
      console.error('Failed to remove submission:', err);
      alert(isBangla ? 'সাবমিশনটি মুছে ফেলতে সমস্যা হয়েছে।' : 'Failed to delete submission. Please try again.');
    } finally {
      setDeletingId(null);
    }
  }

  function handleSelectCategory(cat: CaptureCategory) {
    setSelectedCategory(cat);
    setCaptureError(null);
    const found = categories.find((c) => c.key === cat);
    if (found) {
      setManualUnit(isBangla ? found.defaultUnitBn : found.defaultUnitEn);
    }
  }

  async function processFile(file: File) {
    const activeCategory = selectedCategory || detectCategoryFromFileName(file.name);
    setSelectedCategory(activeCategory);
    setCaptureError(null);
    setIsProcessing(true);

    try {
      // 1. Client-side crop/resize to max 1600px, compress <400KB, strip EXIF GPS (or bypass if PDF)
      const processed = await processAndCompressImage(file, file.name);

      // 2. Enqueue into IndexedDB
      const queued = await enqueueCapture({
        id: processed.idempotencyKey,
        category: activeCategory,
        blob: processed.blob,
        fileName: processed.fileName,
        fileSizeBytes: processed.fileSizeBytes,
      });

      const receiptCode = queued.id.slice(0, 8).toUpperCase();
      setConfirmedReceipt({ id: queued.id, code: receiptCode });

      if (isBatchMode) {
        setBatchCount((prev) => prev + 1);
      } else {
        setSelectedCategory(null);
      }

      await loadQueue();

      // Trigger upload immediately if online
      if (navigator.onLine) {
        await processQueueUploads();
        const updatedList = await getAllCaptureItems();
        setSubmissions(updatedList);
        const match = updatedList.find((i) => i.id === queued.id);
        if (match?.serverDocId) {
          setConfirmedReceipt({ id: queued.id, code: receiptCode, serverDocId: match.serverDocId });
        }
      }
    } catch (err: any) {
      console.error('Capture error:', err);
      setCaptureError(err?.message || (isBangla ? 'ডকুমেন্ট আপলোড বা প্রসেসিং করতে সমস্যা হয়েছে। অনুগ্রহ করে আবার চেষ্টা করুন।' : 'Failed to process and upload document. Please try again.'));
    } finally {
      setIsProcessing(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
      if (cameraInputRef.current) cameraInputRef.current.value = '';
    }
  }

  function handleFileCapture(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (file) {
      processFile(file);
    }
  }

  function handleDropFile(e: React.DragEvent) {
    e.preventDefault();
    setIsDragging(false);
    const file = e.dataTransfer.files?.[0];
    if (file) {
      processFile(file);
    }
  }

  // Camera Management Methods using HTML5 getUserMedia
  async function startCamera() {
    setCameraError(null);
    setCapturedPhotoUrl(null);
    setCapturedBlob(null);
    setIsCameraOpen(true);

    try {
      if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
        throw new Error(
          isBangla
            ? 'আপনার ব্রাউজার সরাসরি ক্যামেরা অ্যাক্সেস সমর্থন করে না। অনুগ্রহ করে ফাইল আপলোড ব্যবহার করুন।'
            : 'Your browser does not support live camera access. Please use file upload.'
        );
      }

      const stream = await navigator.mediaDevices.getUserMedia({
        video: {
          facingMode: { ideal: 'environment' },
          width: { ideal: 1920 },
          height: { ideal: 1080 },
        },
        audio: false,
      });

      setCameraStream(stream);
    } catch (err: any) {
      console.warn('getUserMedia camera error:', err);
      if (err.name === 'NotAllowedError' || err.name === 'PermissionDeniedError') {
        setCameraError(
          isBangla
            ? 'ক্যামেরা ব্যবহারের অনুমতি দেওয়া হয়নি। অনুগ্রহ করে ব্রাউজারের ক্যামেরা পারমিশন সক্রিয় করুন।'
            : 'Camera permission denied. Please allow camera access in your browser settings.'
        );
      } else if (err.name === 'NotFoundError' || err.name === 'DevicesNotFoundError') {
        setCameraError(
          isBangla
            ? 'কোনো ক্যামেরা বা ওয়েবক্যাম খুঁজে পাওয়া যায়নি।'
            : 'No camera or webcam device found on your computer.'
        );
      } else {
        setCameraError(
          err.message ||
            (isBangla
              ? 'ক্যামেরা সংযোগ স্থাপন করা যায়নি।'
              : 'Could not connect to camera.')
        );
      }
    }
  }

  function stopCamera() {
    if (cameraStream) {
      cameraStream.getTracks().forEach((track) => track.stop());
      setCameraStream(null);
    }
    if (videoRef.current) {
      videoRef.current.srcObject = null;
    }
    if (capturedPhotoUrl) {
      URL.revokeObjectURL(capturedPhotoUrl);
    }
    setIsCameraOpen(false);
    setCapturedPhotoUrl(null);
    setCapturedBlob(null);
    setCameraError(null);
  }

  function takePhoto() {
    if (!videoRef.current || !canvasRef.current) return;
    const video = videoRef.current;
    const canvas = canvasRef.current;

    canvas.width = video.videoWidth || 1280;
    canvas.height = video.videoHeight || 720;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

    canvas.toBlob(
      (blob) => {
        if (blob) {
          setCapturedBlob(blob);
          setCapturedPhotoUrl(URL.createObjectURL(blob));
        }
      },
      'image/jpeg',
      0.92
    );
  }

  function retakePhoto() {
    if (capturedPhotoUrl) {
      URL.revokeObjectURL(capturedPhotoUrl);
    }
    setCapturedPhotoUrl(null);
    setCapturedBlob(null);
  }

  async function confirmCapturedPhoto() {
    if (!capturedBlob) return;
    const filename = `camera_scan_${Date.now()}.jpg`;
    const file = new File([capturedBlob], filename, { type: 'image/jpeg' });
    stopCamera();
    await processFile(file);
  }

  async function handleManualSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedCategory || !manualQuantity) return;

    setIsProcessing(true);
    setCaptureError(null);
    try {
      const parsedQty = parseFloat(manualQuantity);
      const queued = await enqueueCapture({
        id: crypto.randomUUID(),
        category: selectedCategory,
        manualQuantity: parsedQty,
        manualSlipNumber: manualSlipNumber,
        manualUnit,
      });

      const receiptCode = queued.id.slice(0, 8).toUpperCase();
      setConfirmedReceipt({ id: queued.id, code: receiptCode });
      setSelectedCategory(null);
      setIsManualMode(false);
      setManualQuantity('');
      setManualSlipNumber('');
      await loadQueue();

      if (navigator.onLine) {
        await processQueueUploads();
        await loadQueue();
      }
    } catch (err: any) {
      console.error('Manual capture error:', err);
      setCaptureError(err?.message || (isBangla ? 'ম্যানুয়াল এন্ট্রি সংরক্ষণ করতে সমস্যা হয়েছে।' : 'Failed to save manual entry.'));
    } finally {
      setIsProcessing(false);
    }
  }

  async function handleSubscribePush() {
    const sub = await requestAndSubscribePush();
    if (sub) {
      setPushSubscribed(true);
    }
  }

  return (
    <div className="min-h-screen bg-slate-100 text-slate-900 font-sans pb-16">
      {/* Hidden File Inputs */}
      <input
        ref={fileInputRef}
        type="file"
        accept="image/*,application/pdf,.pdf,.jpg,.jpeg,.png,.webp"
        className="hidden"
        onChange={handleFileCapture}
      />
      <input
        ref={cameraInputRef}
        type="file"
        accept="image/*"
        capture="environment"
        className="hidden"
        onChange={handleFileCapture}
      />

      {/* Hidden Snapshot Canvas */}
      <canvas ref={canvasRef} className="hidden" />

      {/* Top PC Web Navigation Bar */}
      <header className="sticky top-0 z-30 bg-white border-b border-slate-200 shadow-xs">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 h-16 flex items-center justify-between">
          <div className="flex items-center gap-3">
            {onBack && (
              <button
                onClick={onBack}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 text-xs font-bold text-slate-700 bg-slate-100 hover:bg-slate-200 rounded-xl transition"
                title={isBangla ? 'ড্যাশবোর্ডে ফিরে যান' : 'Back to Dashboard'}
              >
                <ArrowLeft size={16} />
                <span className="hidden sm:inline">{isBangla ? 'ফিরে যান' : 'Back'}</span>
              </button>
            )}
            <div>
              <div className="flex items-center gap-2">
                <span className="text-xl font-black text-emerald-800 tracking-tight">
                  {isBangla ? 'কার্বনবিল' : 'CarbonBill'}
                </span>
                <span className="hidden sm:inline text-xs font-bold px-2 py-0.5 bg-emerald-50 text-emerald-700 rounded-md border border-emerald-200">
                  {isBangla ? 'ইনজেশন পোর্টাল' : 'Ingestion Portal'}
                </span>
              </div>
              <p className="text-xs font-medium text-slate-500">
                {isBangla ? 'ফ্যাক্টরি ফ্লোর চালান ও ইউটিলিটি বিল ইনজেশন' : 'Factory Floor Challan & Utility Bill Ingestion'}
              </p>
            </div>
          </div>

          <div className="flex items-center gap-3">
            {/* Language Switcher */}
            <button
              onClick={() => i18n.changeLanguage(isBangla ? 'en' : 'bn')}
              className="text-xs font-semibold px-2.5 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-xl border border-slate-200 transition"
              title="Switch Language / ভাষা পরিবর্তন"
            >
              {isBangla ? 'English' : 'বাংলা'}
            </button>

            {/* Batch Mode Switch */}
            <button
              onClick={() => setIsBatchMode(!isBatchMode)}
              className={`inline-flex items-center gap-1.5 px-3.5 py-2 rounded-xl text-xs font-bold transition shadow-xs ${
                isBatchMode
                  ? 'bg-emerald-700 text-white shadow-emerald-200'
                  : 'bg-white border border-slate-300 text-slate-700 hover:bg-slate-50'
              }`}
              title={isBangla ? 'একের পর এক একাধিক চালান স্ক্যান করতে ব্যাচ মোড ব্যবহার করুন' : 'Enable batch mode to capture multiple challans sequentially'}
            >
              <Layers size={15} />
              <span>{isBangla ? 'ব্যাচ মোড' : 'Batch Mode'}</span>
              {isBatchMode && (
                <span className="bg-emerald-900 text-emerald-100 text-[10px] px-1.5 py-0.2 rounded-full font-mono">
                  {formatLocalizedNumber(batchCount, isBangla)}
                </span>
              )}
            </button>

            {/* Manual Mode Toggle */}
            <button
              onClick={() => setIsManualMode(!isManualMode)}
              className={`inline-flex items-center gap-1.5 px-3.5 py-2 rounded-xl text-xs font-bold transition shadow-xs ${
                isManualMode
                  ? 'bg-amber-600 text-white shadow-amber-200'
                  : 'bg-white border border-slate-300 text-slate-700 hover:bg-slate-50'
              }`}
            >
              <FileText size={15} />
              <span>
                {isManualMode
                  ? (isBangla ? 'স্ক্যান মোডে ফিরুন' : 'Back to Scan Mode')
                  : (isBangla ? 'ম্যানুয়াল এন্ট্রি' : 'Manual Entry')}
              </span>
            </button>

            {/* Online/Offline Status Badge */}
            <span
              className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-semibold shadow-xs ${
                isOnline ? 'bg-emerald-50 text-emerald-700 border border-emerald-200' : 'bg-rose-50 text-rose-700 border border-rose-200'
              }`}
            >
              {isOnline ? <Wifi size={14} className="text-emerald-600" /> : <WifiOff size={14} className="text-rose-600" />}
              <span className="hidden sm:inline">
                {isOnline ? (isBangla ? 'অনলাইন' : 'Online') : (isBangla ? 'অফলাইন' : 'Offline')}
              </span>
            </span>

            {/* Sync Button */}
            <button
              onClick={triggerSync}
              disabled={isProcessing}
              title={isBangla ? 'সার্ভারের সাথে সিঙ্ক করুন' : 'Sync with Server'}
              className="p-2 text-slate-600 hover:bg-slate-100 border border-slate-200 rounded-xl transition"
            >
              <RefreshCw size={16} className={isProcessing ? 'animate-spin text-emerald-600' : ''} />
            </button>

            {/* Push Notification Setup */}
            {!pushSubscribed && (
              <button
                onClick={handleSubscribePush}
                title={isBangla ? 'পুশ নোটিফিকেশন চালু করুন' : 'Enable Push Notifications'}
                className="hidden md:flex p-2 bg-slate-100 hover:bg-slate-200 text-slate-600 rounded-xl text-xs items-center gap-1.5 font-medium transition"
              >
                <Bell size={15} />
              </button>
            )}
          </div>
        </div>
      </header>

      {/* Main Container */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-6">
        {/* Processing Banner */}
        {isProcessing && (
          <div className="mb-6 bg-teal-50 border border-teal-300 rounded-2xl p-4 shadow-sm animate-pulse flex items-center gap-3">
            <RefreshCw size={24} className="text-teal-600 animate-spin shrink-0" />
            <div className="flex-1">
              <h4 className="text-sm font-bold text-teal-950">
                {isBangla ? 'ডকুমেন্ট প্রক্রিয়াকরণ ও ব্যাকগ্রাউন্ড সিঙ্ক চলছে...' : 'Processing Document & Background Sync...'}
              </h4>
              <p className="text-xs text-teal-700">
                {isBangla ? 'ফাইল কম্প্রেস করে সার্ভারে পাঠানো হচ্ছে, অনুগ্রহ করে অপেক্ষা করুন।' : 'Compressing file and transmitting to server, please wait.'}
              </p>
            </div>
          </div>
        )}

        {/* Error Alert Banner */}
        {captureError && (
          <div className="mb-6 bg-rose-50 border border-rose-300 rounded-2xl p-4 shadow-sm flex items-start gap-3">
            <AlertCircle size={24} className="text-rose-600 shrink-0 mt-0.5" />
            <div className="flex-1">
              <h4 className="text-sm font-bold text-rose-950">
                {isBangla ? 'আপলোড ব্যর্থ হয়েছে' : 'Upload Failed'}
              </h4>
              <p className="text-xs text-rose-800 mt-0.5">{captureError}</p>
              <button
                onClick={() => setCaptureError(null)}
                className="mt-2 text-xs font-bold text-rose-700 hover:underline"
              >
                {isBangla ? 'বন্ধ করুন' : 'Close'}
              </button>
            </div>
          </div>
        )}

        {/* Confirmed Receipt Banner */}
        {confirmedReceipt && (
          <div className="mb-6 bg-emerald-50 border-2 border-emerald-400 rounded-2xl p-5 shadow-sm animate-fade-in flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div className="flex items-start gap-3.5">
              <div className="w-10 h-10 rounded-xl bg-emerald-600 text-white flex items-center justify-center shrink-0 shadow-xs">
                <CheckCircle2 size={24} />
              </div>
              <div>
                <h3 className="text-base font-bold text-emerald-950">
                  {isBangla ? 'চালান সফলভাবে জমা দেওয়া হয়েছে!' : 'Challan Successfully Submitted!'}
                </h3>
                <div className="flex flex-wrap items-center gap-2 mt-1">
                  <span className="text-xs text-emerald-800">
                    {isBangla ? 'রসিদ ট্র্যাকিং কোড:' : 'Receipt Tracking Code:'}
                  </span>
                  <span className="font-mono font-black text-sm bg-emerald-200/90 text-emerald-950 px-2.5 py-0.5 rounded-lg border border-emerald-300">
                    {confirmedReceipt.code}
                  </span>
                  <span className="text-xs text-emerald-700">
                    {isBangla ? '— ডকুমেন্টটি হিসাবরক্ষকের পর্যালোচনার জন্য প্রস্তুত।' : '— Document is ready for accountant review.'}
                  </span>
                </div>
              </div>
            </div>

            <div className="flex items-center gap-2.5 shrink-0">
              <a
                href={`/?mode=review&docId=${confirmedReceipt.serverDocId || confirmedReceipt.id}`}
                onClick={(e) => {
                  if (onGoToReview) {
                    e.preventDefault();
                    onGoToReview(confirmedReceipt.serverDocId || confirmedReceipt.id);
                  }
                }}
                className="inline-flex items-center gap-1.5 px-4 py-2 bg-emerald-700 hover:bg-emerald-800 text-white text-xs font-bold rounded-xl shadow-xs transition"
              >
                <ExternalLink size={14} />
                {isBangla ? 'পর্যালোচনা কিউ-তে বিলটি দেখুন' : 'View Bill in Review Queue'}
              </a>
              <button
                onClick={() => setConfirmedReceipt(null)}
                className="px-3 py-2 text-xs font-semibold text-emerald-800 hover:bg-emerald-100 rounded-xl transition"
              >
                {isBangla ? 'বাতিল' : 'Dismiss'}
              </button>
            </div>
          </div>
        )}

        {/* Responsive 2-Column Grid */}
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
          {/* Left Column: Capture & Ingestion Workspace (7 cols on lg) */}
          <div className="lg:col-span-7 space-y-6">
            {isManualMode ? (
              /* Manual Entry Card */
              <div className="bg-white border border-slate-200 rounded-2xl p-6 shadow-xs">
                <div className="flex items-center justify-between mb-4 pb-3 border-b border-slate-100">
                  <div className="flex items-center gap-2.5">
                    <div className="w-9 h-9 rounded-xl bg-amber-100 text-amber-700 flex items-center justify-center">
                      <FileText size={20} />
                    </div>
                    <div>
                      <h2 className="text-base font-bold text-slate-900">
                        {isBangla ? 'ম্যানুয়াল ফলব্যাক এন্ট্রি' : 'Manual Fallback Entry'}
                      </h2>
                      <p className="text-xs text-slate-500">
                        {isBangla ? 'চালান বা বিলের ছবি অস্পষ্ট হলে সরাসরি তথ্য প্রদান করুন' : 'Enter details directly if bill photo or scan is unclear'}
                      </p>
                    </div>
                  </div>
                  <span className="text-xs font-bold px-2.5 py-1 bg-amber-50 text-amber-800 rounded-lg border border-amber-200">
                    {isBangla ? 'ম্যানুয়াল মোড' : 'Manual Mode'}
                  </span>
                </div>

                <form onSubmit={handleManualSubmit} className="space-y-4">
                  <div>
                    <label className="block text-xs font-bold text-slate-700 mb-2">
                      {isBangla ? '১. ক্যাটাগরি নির্বাচন করুন' : '1. Select Category'}
                    </label>
                    <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-2.5">
                      {categories.map((c) => {
                        const Icon = c.icon;
                        const isSelected = selectedCategory === c.key;
                        return (
                          <button
                            key={c.key}
                            type="button"
                            onClick={() => {
                              setSelectedCategory(c.key);
                              setManualUnit(isBangla ? c.defaultUnitBn : c.defaultUnitEn);
                            }}
                            className={`p-3 rounded-xl text-xs font-bold border text-left flex flex-col gap-1.5 transition ${
                              isSelected
                                ? 'border-emerald-600 bg-emerald-50 text-emerald-900 shadow-xs ring-2 ring-emerald-400'
                                : 'border-slate-200 bg-slate-50 hover:bg-slate-100 text-slate-700'
                            }`}
                          >
                            <div className="flex items-center gap-1.5">
                              <Icon size={16} className={isSelected ? 'text-emerald-700' : 'text-slate-500'} />
                              <span>{isBangla ? c.labelBn : c.labelEn}</span>
                            </div>
                            <span className="text-[10px] text-slate-500 font-normal">
                              {isBangla ? c.defaultUnitBn : c.defaultUnitEn}
                            </span>
                          </button>
                        );
                      })}
                    </div>
                  </div>

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-bold text-slate-700 mb-1.5">
                        {isBangla ? `২. পরিমাণ (${manualUnit})` : `2. Quantity (${manualUnit})`} <span className="text-rose-500">*</span>
                      </label>
                      <div className="relative">
                        <input
                          type="number"
                          step="any"
                          required
                          value={manualQuantity}
                          onChange={(e) => setManualQuantity(e.target.value)}
                          placeholder={isBangla ? 'যেমন: ৩,৫০০' : 'e.g. 3,500'}
                          className="w-full px-3.5 py-2.5 rounded-xl border border-slate-300 text-base font-bold text-slate-900 focus:outline-hidden focus:border-emerald-600 focus:ring-2 focus:ring-emerald-100 transition"
                        />
                        <span className="absolute right-3.5 top-3 text-xs font-semibold text-slate-400">
                          {manualUnit}
                        </span>
                      </div>
                    </div>

                    <div>
                      <label className="block text-xs font-bold text-slate-700 mb-1.5">
                        {isBangla ? '৩. স্লিপ / চালান নম্বর (ঐচ্ছিক)' : '3. Slip / Challan Number (Optional)'}
                      </label>
                      <input
                        type="text"
                        value={manualSlipNumber}
                        onChange={(e) => setManualSlipNumber(e.target.value)}
                        placeholder={isBangla ? 'যেমন: POCL-44912' : 'e.g. POCL-44912'}
                        className="w-full px-3.5 py-2.5 rounded-xl border border-slate-300 text-sm font-medium text-slate-900 focus:outline-hidden focus:border-emerald-600 focus:ring-2 focus:ring-emerald-100 transition"
                      />
                    </div>
                  </div>

                  <div className="pt-2 flex items-center gap-3">
                    <button
                      type="submit"
                      disabled={isProcessing || !selectedCategory || !manualQuantity}
                      className="flex-1 py-3 bg-amber-600 hover:bg-amber-700 text-white font-bold rounded-xl text-sm shadow-md transition disabled:opacity-50"
                    >
                      {isProcessing
                        ? (isBangla ? 'সংরক্ষণ হচ্ছে...' : 'Saving...')
                        : (isBangla ? 'সংরক্ষণ করুন ও কিউ-তে জমা দিন' : 'Save & Submit to Queue')}
                    </button>
                    <button
                      type="button"
                      onClick={() => setIsManualMode(false)}
                      className="px-4 py-3 bg-slate-100 hover:bg-slate-200 text-slate-700 font-bold rounded-xl text-sm transition"
                    >
                      {isBangla ? 'বাতিল' : 'Cancel'}
                    </button>
                  </div>
                </form>
              </div>
            ) : (
              /* PC Web File Ingestion Workspace */
              <div className="bg-white border border-slate-200 rounded-2xl p-6 shadow-xs space-y-6">
                {/* Step 1: Category Selection */}
                <div>
                  <div className="flex items-center justify-between mb-3">
                    <div>
                      <h3 className="text-sm font-bold text-slate-900">
                        {isBangla ? '১. জ্বালানি বা ইউটিলিটি ক্যাটাগরি নির্বাচন করুন' : '1. Select Fuel or Utility Category'}
                      </h3>
                      <p className="text-xs text-slate-500">
                        {isBangla
                          ? 'ক্যাটাগরি আগে নির্বাচন না করলেও ফাইলের নাম অনুযায়ী স্বয়ংক্রিয়ভাবে শনাক্ত হবে'
                          : 'Auto-detects from filename if category is not pre-selected'}
                      </p>
                    </div>
                    {selectedCategory && (
                      <button
                        onClick={() => setSelectedCategory(null)}
                        className="text-xs font-semibold text-emerald-700 hover:underline"
                      >
                        {isBangla ? 'ক্যাটাগরি পরিবর্তন' : 'Change Category'}
                      </button>
                    )}
                  </div>

                  <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-3">
                    {categories.map((cat) => {
                      const Icon = cat.icon;
                      const isSelected = selectedCategory === cat.key;

                      return (
                        <button
                          key={cat.key}
                          type="button"
                          onClick={() => handleSelectCategory(cat.key)}
                          className={`relative flex flex-col items-center justify-center p-3.5 rounded-2xl border-2 transition text-center shadow-xs ${
                            isSelected
                              ? `${cat.activeBorder} ${cat.accentBg} shadow-sm`
                              : 'border-slate-200 bg-white hover:border-slate-300 hover:bg-slate-50'
                          }`}
                        >
                          <div className={`w-11 h-11 rounded-xl ${cat.color} flex items-center justify-center mb-2 shadow-xs`}>
                            <Icon size={22} strokeWidth={2.2} />
                          </div>
                          <span className="text-sm font-bold text-slate-900 tracking-tight leading-tight">
                            {isBangla ? cat.labelBn : cat.labelEn}
                          </span>
                          <span className="text-[11px] font-medium text-slate-500 mt-0.5">
                            {isBangla ? cat.subBn : cat.subEn}
                          </span>
                        </button>
                      );
                    })}
                  </div>
                </div>

                {/* Step 2: PC Drag & Drop Dropzone */}
                <div>
                  <h3 className="text-sm font-bold text-slate-900 mb-2">
                    {isBangla ? '২. চালান বা বিলের ডকুমেন্ট আপলোড করুন' : '2. Upload Challan or Bill Document'}
                  </h3>

                  <div
                    onDragOver={(e) => {
                      e.preventDefault();
                      setIsDragging(true);
                    }}
                    onDragLeave={() => setIsDragging(false)}
                    onDrop={handleDropFile}
                    className={`border-2 border-dashed rounded-2xl p-8 text-center transition-all ${
                      isDragging
                        ? 'border-emerald-500 bg-emerald-50/80 scale-[1.01]'
                        : 'border-slate-300 bg-slate-50/60 hover:bg-slate-50 hover:border-slate-400'
                    }`}
                  >
                    <div className="w-16 h-16 rounded-2xl bg-white border border-slate-200 shadow-xs text-emerald-700 flex items-center justify-center mx-auto mb-3.5">
                      <Upload size={30} strokeWidth={2} />
                    </div>

                    <h4 className="text-base font-bold text-slate-900 mb-1">
                      {isBangla ? 'চালান বা বিলের পিডিএফ (PDF) অথবা ছবি ড্র্যাগ করে এখানে ছাড়ুন' : 'Drag and drop challan/bill PDF or image here'}
                    </h4>
                    <p className="text-xs text-slate-500 max-w-md mx-auto mb-5">
                      {isBangla
                        ? 'কম্পিউটার থেকে সরাসরি ইনভয়েস, ক্যাশ মেমো, তিতাস/ডেসকো বিল বা ডেলিভারি চালান ড্রপ করুন'
                        : 'Drop invoices, cash memos, utility bills, or delivery challans directly from your computer'}
                    </p>

                    <div className="flex flex-wrap items-center justify-center gap-3">
                      <button
                        type="button"
                        onClick={() => fileInputRef.current?.click()}
                        disabled={isProcessing}
                        className="px-5 py-2.5 bg-emerald-700 hover:bg-emerald-800 text-white rounded-xl font-bold text-xs flex items-center gap-2 shadow-xs transition active:scale-95 cursor-pointer"
                      >
                        <Upload size={16} />
                        {isBangla ? 'কম্পিউটার থেকে ফাইল বাছুন' : 'Browse Files from PC'}
                      </button>

                      <button
                        type="button"
                        onClick={startCamera}
                        disabled={isProcessing}
                        className="px-4 py-2.5 bg-slate-800 hover:bg-slate-900 text-white rounded-xl font-bold text-xs flex items-center gap-2 shadow-xs transition active:scale-95 cursor-pointer"
                      >
                        <Camera size={16} />
                        {isBangla ? 'ক্যামেরা দিয়ে স্ক্যান' : 'Scan with Camera'}
                      </button>
                    </div>

                    <p className="text-[11px] text-slate-400 font-medium mt-4">
                      {isBangla
                        ? 'সমর্থিত ফরম্যাট: PDF, JPG, PNG, WEBP (ক্লাইন্ট-সাইড নিরাপদ অটো-কম্প্রেশন)'
                        : 'Supported formats: PDF, JPG, PNG, WEBP (secure client-side auto-compression)'}
                    </p>
                  </div>
                </div>
              </div>
            )}
          </div>

          {/* Right Column: Submitted Submissions Queue (5 cols on lg) */}
          <div className="lg:col-span-5">
            <div className="bg-white border border-slate-200 rounded-2xl p-6 shadow-xs flex flex-col h-full">
              <div className="flex items-center justify-between mb-4 pb-3 border-b border-slate-100">
                <div>
                  <h2 className="text-base font-bold text-slate-900">
                    {isBangla ? 'আমার জমা দেওয়া চালানসমূহ' : 'My Submissions'}
                  </h2>
                  <p className="text-xs text-slate-500">
                    {isBangla ? 'অফলাইন ও অনলাইন ইনজেকশন হিস্ট্রি' : 'Offline & Online Ingestion History'}
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <span className="text-xs font-bold px-2.5 py-0.5 bg-slate-100 text-slate-700 rounded-full border border-slate-200">
                    {isBangla
                      ? `মোট: ${formatLocalizedNumber(submissions.length, true)} টি`
                      : `Total: ${formatLocalizedNumber(submissions.length, false)}`}
                  </span>
                  <button
                    onClick={loadQueue}
                    title={isBangla ? 'রিফ্রেশ করুন' : 'Refresh'}
                    className="p-1.5 text-slate-500 hover:bg-slate-100 rounded-lg transition"
                  >
                    <RefreshCw size={14} />
                  </button>
                </div>
              </div>

              {/* Submissions List */}
              {submissions.length === 0 ? (
                <div className="flex-1 flex flex-col items-center justify-center py-12 text-center">
                  <div className="w-12 h-12 rounded-2xl bg-slate-100 text-slate-400 flex items-center justify-center mb-3">
                    <FileText size={24} />
                  </div>
                  <h4 className="text-sm font-bold text-slate-700">
                    {isBangla ? 'এখনও কোনো চালান জমা দেওয়া হয়নি' : 'No challans submitted yet'}
                  </h4>
                  <p className="text-xs text-slate-400 mt-1 max-w-xs">
                    {isBangla
                      ? 'বামপাশের ড্রপজোন বা আপলোড বাটন ব্যবহার করে প্রথম চালানটি জমা দিন।'
                      : 'Use the dropzone or upload button on the left to submit your first challan.'}
                  </p>
                </div>
              ) : (
                <div className="space-y-2.5 overflow-y-auto max-h-[580px] pr-1">
                  {submissions.map((sub) => {
                    const isReceived = sub.status === 'received';
                    const isUploading = sub.status === 'uploading';
                    const isFailed = sub.status === 'failed';

                    return (
                      <div
                        key={sub.id}
                        className="bg-slate-50/70 hover:bg-slate-50 border border-slate-200/90 rounded-xl p-3.5 flex items-center justify-between gap-3 transition"
                      >
                        <div className="flex items-center gap-3 min-w-0">
                          <div className="w-9 h-9 rounded-xl bg-white border border-slate-200 shadow-2xs flex items-center justify-center shrink-0">
                            {sub.category === 'diesel' && <Fuel size={18} className="text-amber-600" />}
                            {sub.category === 'gas' && <Flame size={18} className="text-orange-600" />}
                            {sub.category === 'electricity' && <Zap size={18} className="text-yellow-600" />}
                            {sub.category === 'shipment' && <Truck size={18} className="text-blue-600" />}
                            {sub.category === 'other' && <MoreHorizontal size={18} className="text-purple-600" />}
                          </div>

                          <div className="min-w-0">
                            <div className="flex items-center gap-2">
                              <span className="text-xs font-bold text-slate-900 font-mono tracking-tight bg-slate-200/80 px-1.5 py-0.2 rounded text-[11px]">
                                {sub.receiptCode || sub.id.slice(0, 8).toUpperCase()}
                              </span>
                              <span className="text-[11px] text-slate-400">
                                {formatLocalizedDate(sub.createdAt, isBangla)}
                              </span>
                            </div>
                            <p className="text-xs text-slate-700 font-medium truncate mt-0.5" title={sub.fileName || (isBangla ? 'ম্যানুয়াল চালান' : 'Manual Challan')}>
                              {sub.manualQuantity
                                ? (isBangla
                                    ? `ম্যানুয়াল: ${formatLocalizedNumber(sub.manualQuantity, true)} ${sub.manualUnit || ''}`
                                    : `Manual: ${formatLocalizedNumber(sub.manualQuantity, false)} ${sub.manualUnit || ''}`)
                                : sub.fileName || (isBangla ? 'ক্যামেরা ফটো' : 'Camera Photo')}
                            </p>
                            {isFailed && sub.error && (
                              <p className="text-[10px] text-rose-600 font-medium truncate" title={sub.error}>
                                {sub.error}
                              </p>
                            )}
                          </div>
                        </div>

                        {/* Status Badges & Action Buttons */}
                        <div className="shrink-0 flex items-center gap-1.5">
                          {sub.status === 'calculated' && (
                            <>
                              <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-[11px] font-bold bg-emerald-100 text-emerald-800 border border-emerald-200 shadow-2xs">
                                <CheckCircle2 size={12} className="text-emerald-700" />
                                {isBangla ? 'যাচাইকৃত ও হিসাবকৃত' : 'Verified & Calculated'}
                              </span>
                              <a
                                href={`/?mode=review&docId=${sub.serverDocId || sub.id}`}
                                onClick={(e) => {
                                  if (onGoToReview) {
                                    e.preventDefault();
                                    onGoToReview(sub.serverDocId || sub.id);
                                  }
                                }}
                                className="px-2.5 py-1 rounded-lg text-[11px] font-bold bg-teal-800 hover:bg-teal-900 text-white flex items-center gap-1 shadow-2xs transition active:scale-95 cursor-pointer"
                                title={isBangla ? 'যাচাইকৃত চালান দেখুন' : 'View verified invoice'}
                              >
                                <ExternalLink size={11} /> {isBangla ? 'দেখুন' : 'View'}
                              </a>
                            </>
                          )}
                          {sub.status === 'needs_review' && (
                            <>
                              <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-[11px] font-bold bg-amber-100 text-amber-800 border border-amber-200">
                                <Clock size={12} className="text-amber-700" />
                                {isBangla ? 'পর্যালোচনার অপেক্ষায়' : 'Needs Review'}
                              </span>
                              <a
                                href={`/?mode=review&docId=${sub.serverDocId || sub.id}`}
                                onClick={(e) => {
                                  if (onGoToReview) {
                                    e.preventDefault();
                                    onGoToReview(sub.serverDocId || sub.id);
                                  }
                                }}
                                className="px-2.5 py-1 rounded-lg text-[11px] font-bold bg-amber-600 hover:bg-amber-700 text-white flex items-center gap-1 shadow-2xs transition active:scale-95 cursor-pointer"
                                title={isBangla ? 'পর্যালোচনা কিউ-তে বিলটি যাচাই করুন' : 'Verify bill in review queue'}
                              >
                                <ExternalLink size={11} /> {isBangla ? 'যাচাই' : 'Verify'}
                              </a>
                            </>
                          )}
                          {sub.status === 'duplicate' && (
                            <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-[11px] font-bold bg-purple-100 text-purple-800 border border-purple-200">
                              {isBangla ? 'ডুপ্লিকেট' : 'Duplicate'}
                            </span>
                          )}
                          {isReceived && (
                            <div className="flex items-center gap-1.5">
                              <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-[11px] font-bold bg-teal-100 text-teal-800 border border-teal-200">
                                <CheckCircle2 size={12} /> {isBangla ? 'গৃহীত' : 'Received'}
                              </span>
                              <a
                                href={`/?mode=review&docId=${sub.serverDocId || sub.id}`}
                                onClick={(e) => {
                                  if (onGoToReview) {
                                    e.preventDefault();
                                    onGoToReview(sub.serverDocId || sub.id);
                                  }
                                }}
                                className="px-2.5 py-1 rounded-lg text-[11px] font-bold bg-emerald-700 hover:bg-emerald-800 text-white flex items-center gap-1 shadow-2xs transition active:scale-95 cursor-pointer"
                                title={isBangla ? 'পর্যালোচনা কিউ-তে বিলটি দেখুন' : 'View bill in review queue'}
                              >
                                <ExternalLink size={11} /> {isBangla ? 'দেখুন' : 'View'}
                              </a>
                            </div>
                          )}
                          {isUploading && (
                            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-bold bg-sky-100 text-sky-800 animate-pulse border border-sky-200">
                              <RefreshCw size={12} className="animate-spin" /> {isBangla ? 'আপলোড হচ্ছে' : 'Uploading'}
                            </span>
                          )}
                          {sub.status === 'queued' && (
                            <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-bold bg-amber-100 text-amber-800 border border-amber-200">
                              <Clock size={12} /> {isBangla ? 'অপেক্ষমান' : 'Queued'}
                            </span>
                          )}
                          {isFailed && (
                            <div className="flex items-center gap-1.5">
                              <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-[11px] font-bold bg-rose-100 text-rose-800 border border-rose-200">
                                <AlertCircle size={12} /> {isBangla ? 'ব্যর্থ' : 'Failed'}
                              </span>
                              <button
                                type="button"
                                onClick={() => handleRetry(sub.id)}
                                className="px-2.5 py-1 rounded-lg text-[11px] font-bold bg-teal-600 hover:bg-teal-700 text-white flex items-center gap-1 shadow-2xs transition active:scale-95 cursor-pointer"
                                title={isBangla ? 'পুনরায় আপলোড চেষ্টা করুন' : 'Retry upload'}
                              >
                                <RefreshCw size={11} /> {isBangla ? 'পুনরায়' : 'Retry'}
                              </button>
                            </div>
                          )}

                          {/* Remove Submission Button */}
                          <button
                            type="button"
                            disabled={deletingId === sub.id}
                            onClick={() => handleRemoveSubmission(sub)}
                            className="p-1.5 rounded-lg text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition active:scale-95 cursor-pointer ml-1 disabled:opacity-50"
                            title={isBangla ? 'সাবমিশনটি মুছে ফেলুন' : 'Remove submission'}
                            aria-label={isBangla ? 'সাবমিশন মুছে ফেলুন' : 'Remove submission'}
                          >
                            {deletingId === sub.id ? (
                              <RefreshCw size={13} className="animate-spin text-rose-500" />
                            ) : (
                              <Trash2 size={13} />
                            )}
                          </button>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>
        </div>
      </main>

      {/* Live Camera Viewfinder Modal for PC & Mobile */}
      {isCameraOpen && (
        <div className="fixed inset-0 z-50 bg-black/85 backdrop-blur-md flex items-center justify-center p-4">
          <div className="bg-slate-900 border border-slate-700 rounded-3xl overflow-hidden max-w-2xl w-full text-white shadow-2xl flex flex-col">
            {/* Camera Modal Header */}
            <div className="px-5 py-4 border-b border-slate-800 flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Camera size={20} className="text-emerald-400" />
                <h3 className="font-bold text-sm text-slate-100">
                  {isBangla ? 'লাইভ ক্যামেরা স্ক্যানার' : 'Live Camera Scanner'}
                </h3>
              </div>
              <button
                type="button"
                onClick={stopCamera}
                className="p-1.5 text-slate-400 hover:text-white hover:bg-slate-800 rounded-xl transition"
                title={isBangla ? 'বন্ধ করুন' : 'Close'}
              >
                <X size={20} />
              </button>
            </div>

            {/* Viewfinder / Preview Screen */}
            <div className="relative bg-black flex items-center justify-center min-h-[360px] max-h-[500px] overflow-hidden">
              {capturedPhotoUrl ? (
                /* Freeze Frame Photo Preview */
                <img
                  src={capturedPhotoUrl}
                  alt="Captured Challan"
                  className="w-full h-full object-contain max-h-[500px]"
                />
              ) : cameraError ? (
                /* Error View if Webcam unavailable / denied */
                <div className="p-8 text-center max-w-md">
                  <div className="w-14 h-14 rounded-2xl bg-rose-900/60 text-rose-400 flex items-center justify-center mx-auto mb-3">
                    <AlertCircle size={28} />
                  </div>
                  <h4 className="font-bold text-base text-rose-200 mb-1.5">
                    {isBangla ? 'ক্যামেরা সংযোগ ব্যর্থ হয়েছে' : 'Camera Access Failed'}
                  </h4>
                  <p className="text-xs text-slate-300 mb-6 leading-relaxed">
                    {cameraError}
                  </p>
                  <div className="flex items-center justify-center gap-3">
                    <button
                      type="button"
                      onClick={() => {
                        stopCamera();
                        fileInputRef.current?.click();
                      }}
                      className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-bold transition shadow-xs"
                    >
                      {isBangla ? 'ফাইল ম্যানেজার থেকে বাছুন' : 'Select from File Manager'}
                    </button>
                    <button
                      type="button"
                      onClick={stopCamera}
                      className="px-4 py-2 bg-slate-800 hover:bg-slate-700 text-slate-300 rounded-xl text-xs font-bold transition"
                    >
                      {isBangla ? 'বন্ধ করুন' : 'Close'}
                    </button>
                  </div>
                </div>
              ) : (
                /* Live Video Stream View */
                <div className="relative w-full h-full flex items-center justify-center">
                  <video
                    ref={videoRef}
                    autoPlay
                    playsInline
                    muted
                    className="w-full h-full object-cover min-h-[360px]"
                  />
                  {/* Viewfinder Target Framing Guidelines */}
                  <div className="absolute inset-8 border-2 border-dashed border-emerald-400/60 rounded-2xl pointer-events-none flex items-center justify-center">
                    <span className="text-[11px] font-bold bg-black/60 text-emerald-300 px-3 py-1 rounded-full backdrop-blur-xs">
                      {isBangla ? 'বিল বা চালানটি ফ্রেমের মধ্যে রাখুন' : 'Position bill or challan within frame'}
                    </span>
                  </div>
                </div>
              )}
            </div>

            {/* Camera Modal Bottom Controls */}
            <div className="px-6 py-4 bg-slate-950 border-t border-slate-800 flex items-center justify-between">
              {capturedPhotoUrl ? (
                /* Retake or Confirm Action Buttons */
                <div className="flex items-center justify-between w-full">
                  <button
                    type="button"
                    onClick={retakePhoto}
                    className="inline-flex items-center gap-2 px-4 py-2.5 bg-slate-800 hover:bg-slate-700 text-slate-200 rounded-xl text-xs font-bold transition"
                  >
                    <RotateCcw size={15} />
                    {isBangla ? 'পুনরায় তুলুন' : 'Retake Photo'}
                  </button>
                  <button
                    type="button"
                    onClick={confirmCapturedPhoto}
                    className="inline-flex items-center gap-2 px-5 py-2.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-xl text-xs font-bold shadow-md transition"
                  >
                    <CheckCircle2 size={16} />
                    {isBangla ? 'এই ছবিটি ব্যবহার করুন' : 'Use This Photo'}
                  </button>
                </div>
              ) : !cameraError ? (
                /* Shutter Button */
                <div className="flex items-center justify-center w-full relative">
                  <button
                    type="button"
                    onClick={takePhoto}
                    className="w-16 h-16 rounded-full bg-white hover:bg-slate-100 text-slate-900 border-4 border-slate-700 flex items-center justify-center shadow-lg active:scale-95 transition"
                    title={isBangla ? 'ছবি তুলুন' : 'Take Photo'}
                  >
                    <div className="w-11 h-11 rounded-full bg-emerald-600 hover:bg-emerald-700 text-white flex items-center justify-center shadow-inner">
                      <Camera size={22} />
                    </div>
                  </button>
                </div>
              ) : (
                <div className="text-right w-full">
                  <button
                    type="button"
                    onClick={stopCamera}
                    className="px-4 py-2 text-xs font-semibold text-slate-400 hover:text-white"
                  >
                    {isBangla ? 'বাতিল' : 'Cancel'}
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default CaptureScreen;
