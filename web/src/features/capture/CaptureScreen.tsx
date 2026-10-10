import React, { useState, useEffect, useRef } from 'react';
import {
  Fuel,
  Flame,
  Zap,
  Truck,
  Camera,
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
} from 'lucide-react';
import {
  enqueueCapture,
  getAllCaptureItems,
  processQueueUploads,
  retryCaptureItem,
  CaptureQueueItem,
} from './offlineCaptureQueue';
import { processAndCompressImage } from './imageProcessing';
import { requestAndSubscribePush } from './pushSubscription';
import { formatBanglaNumber, formatBanglaDate } from '../../shared/formatters';

export function CaptureScreen({ onBack }: { onBack?: () => void } = {}) {
  const [selectedCategory, setSelectedCategory] = useState<'diesel' | 'gas' | 'electricity' | 'shipment' | null>(null);
  const [isBatchMode, setIsBatchMode] = useState<boolean>(false);
  const [batchCount, setBatchCount] = useState<number>(0);
  const [isManualMode, setIsManualMode] = useState<boolean>(false);
  const [manualQuantity, setManualQuantity] = useState<string>('');
  const [manualSlipNumber, setManualSlipNumber] = useState<string>('');
  const [manualUnit, setManualUnit] = useState<string>('litre');
  const [confirmedReceipt, setConfirmedReceipt] = useState<{ id: string; code: string } | null>(null);
  const [submissions, setSubmissions] = useState<CaptureQueueItem[]>([]);
  const [isProcessing, setIsProcessing] = useState<boolean>(false);
  const [isOnline, setIsOnline] = useState<boolean>(typeof navigator !== 'undefined' ? navigator.onLine : true);
  const [pushSubscribed, setPushSubscribed] = useState<boolean>(false);

  const fileInputRef = useRef<HTMLInputElement>(null);

  const categories = [
    {
      key: 'diesel' as const,
      labelBn: 'ডিজেল স্লিপ',
      subBn: 'জেনারেটর ও বয়লার ফুয়েল',
      icon: Fuel,
      color: 'bg-amber-600',
      activeBorder: 'border-amber-600',
      defaultUnit: 'লিটার',
    },
    {
      key: 'gas' as const,
      labelBn: 'গ্যাস বিল',
      subBn: 'তিতাস / বাখরাবাদ আরএমএস',
      icon: Flame,
      color: 'bg-orange-600',
      activeBorder: 'border-orange-600',
      defaultUnit: 'ঘনমিটার',
    },
    {
      key: 'electricity' as const,
      labelBn: 'বিদ্যুৎ বিল',
      subBn: 'ডেসকো / ডিপিডিসি / আরইবি',
      icon: Zap,
      color: 'bg-yellow-600',
      activeBorder: 'border-yellow-600',
      defaultUnit: 'কেডব্লিউএইচ',
    },
    {
      key: 'shipment' as const,
      labelBn: 'পরিবহন চালান',
      subBn: 'ট্রাক ও কার্গো চালান',
      icon: Truck,
      color: 'bg-blue-600',
      activeBorder: 'border-blue-600',
      defaultUnit: 'কেজি',
    },
  ];

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
    };
  }, []);

  async function loadQueue() {
    const list = await getAllCaptureItems();
    setSubmissions(list);
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

  function handleSelectCategory(cat: 'diesel' | 'gas' | 'electricity' | 'shipment') {
    setSelectedCategory(cat);
    const found = categories.find((c) => c.key === cat);
    if (found) setManualUnit(found.defaultUnit);

    // Auto-trigger camera file selector for 2-tap UX
    if (!isManualMode) {
      setTimeout(() => {
        fileInputRef.current?.click();
      }, 50);
    }
  }

  async function handleFileCapture(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file || !selectedCategory) return;

    setIsProcessing(true);
    try {
      // 1. Client-side crop/resize to max 1600px, compress <400KB, strip EXIF GPS
      const processed = await processAndCompressImage(file, file.name);

      // 2. Enqueue into IndexedDB
      const queued = await enqueueCapture({
        id: processed.idempotencyKey,
        category: selectedCategory,
        blob: processed.blob,
        fileName: processed.fileName,
        fileSizeBytes: processed.fileSizeBytes,
      });

      const receiptCode = queued.id.slice(0, 8).toUpperCase();
      setConfirmedReceipt({ id: queued.id, code: receiptCode });

      if (isBatchMode) {
        setBatchCount((prev) => prev + 1);
        // Remain in category ready for next shot
      } else {
        setSelectedCategory(null);
      }

      await loadQueue();

      // Trigger upload if online
      if (navigator.onLine) {
        triggerSync();
      }
    } catch (err) {
      console.error('Capture error:', err);
    } finally {
      setIsProcessing(false);
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  }

  async function handleManualSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedCategory || !manualQuantity) return;

    setIsProcessing(true);
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
        triggerSync();
      }
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
    <div className="max-w-md mx-auto min-h-screen bg-slate-50 text-slate-900 font-sans pb-16">
      {/* Hidden Native Camera Input */}
      <input
        ref={fileInputRef}
        type="file"
        accept="image/*"
        capture="environment"
        className="hidden"
        onChange={handleFileCapture}
      />

      {/* Top App Header */}
      <header className="sticky top-0 z-20 bg-white/95 backdrop-blur border-b border-slate-200 px-4 py-3 flex items-center justify-between shadow-xs">
        <div className="flex items-center gap-2">
          {onBack && (
            <button
              onClick={onBack}
              className="p-1.5 -ml-1 text-slate-600 hover:bg-slate-100 rounded-lg transition"
              title="ফিরে যান"
            >
              <ArrowLeft size={20} />
            </button>
          )}
          <div>
            <h1 className="text-xl font-black text-emerald-800 tracking-tight">কার্বনবিল</h1>
            <p className="text-xs font-medium text-slate-500">ফ্যাক্টরি ফ্লোর ইনজেকশন</p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {/* Online/Offline Badge */}
          <span
            className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold ${
              isOnline ? 'bg-emerald-100 text-emerald-800' : 'bg-rose-100 text-rose-800'
            }`}
          >
            {isOnline ? <Wifi size={13} /> : <WifiOff size={13} />}
            {isOnline ? 'অনলাইন' : 'অফলাইন'}
          </span>

          {/* Sync Button */}
          <button
            onClick={triggerSync}
            disabled={isProcessing}
            title="সিঙ্ক করুন"
            className="p-2 text-slate-600 hover:bg-slate-100 rounded-lg transition"
          >
            <RefreshCw size={17} className={isProcessing ? 'animate-spin text-emerald-600' : ''} />
          </button>
        </div>
      </header>

      {/* Mode Switches & VAPID Push Alert */}
      <div className="px-4 pt-3 flex items-center justify-between gap-2">
        <button
          onClick={() => setIsBatchMode(!isBatchMode)}
          className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold transition ${
            isBatchMode
              ? 'bg-emerald-700 text-white shadow-xs'
              : 'bg-white border border-slate-300 text-slate-700'
          }`}
        >
          <Layers size={14} />
          ব্যাচ মোড {isBatchMode && `(${formatBanglaNumber(batchCount)})`}
        </button>

        <button
          onClick={() => setIsManualMode(!isManualMode)}
          className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-bold transition ${
            isManualMode
              ? 'bg-amber-600 text-white shadow-xs'
              : 'bg-white border border-slate-300 text-slate-700'
          }`}
        >
          <FileText size={14} />
          ম্যানুয়াল এন্ট্রি
        </button>

        {!pushSubscribed && (
          <button
            onClick={handleSubscribePush}
            title="নোটিফিকেশন অন করুন"
            className="p-1.5 bg-slate-100 text-slate-600 hover:text-emerald-700 rounded-lg text-xs flex items-center gap-1"
          >
            <Bell size={14} />
          </button>
        )}
      </div>

      {/* Instant Receipt Confirmation Banner */}
      {confirmedReceipt && (
        <div className="m-4 bg-emerald-50 border-2 border-emerald-500 rounded-2xl p-4 shadow-sm animate-fade-in flex items-start gap-3">
          <CheckCircle2 size={32} className="text-emerald-600 shrink-0 mt-0.5" />
          <div className="flex-1">
            <h3 className="text-base font-bold text-emerald-950">রসিদ নিশ্চিতকরণ গৃহীত!</h3>
            <p className="text-xs text-emerald-800 mt-0.5">
              ট্র্যাকিং কোড: <span className="font-mono font-black text-sm bg-emerald-200/80 px-2 py-0.5 rounded">{confirmedReceipt.code}</span>
            </p>
            <p className="text-[11px] text-emerald-700 mt-1">
              ডকুমেন্টটি সফলভাবে সংরক্ষিত হয়েছে এবং হিসাবরক্ষকের যাচাইয়ের জন্য প্রস্তুত।
            </p>
            <div className="mt-2.5 flex items-center gap-2">
              <a
                href={`/?mode=review&docId=${confirmedReceipt.id}`}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-emerald-700 hover:bg-emerald-800 text-white text-xs font-bold rounded-xl shadow-sm transition"
              >
                📋 পর্যালোচনা কিউ-তে বিলটি দেখুন (View Extracted Bill ➔)
              </a>
            </div>
          </div>
        </div>
      )}

      {/* Manual Entry Form */}
      {isManualMode ? (
        <div className="m-4 bg-white border border-slate-200 rounded-2xl p-4 shadow-sm">
          <h2 className="text-base font-bold text-slate-900 mb-3 flex items-center gap-2">
            <FileText size={18} className="text-amber-600" />
            ম্যানুয়াল ফলব্যাক এন্ট্রি (ক্যামেরা অস্পষ্ট হলে)
          </h2>

          <form onSubmit={handleManualSubmit} className="space-y-3">
            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">ক্যাটাগরি নির্বাচন</label>
              <div className="grid grid-cols-2 gap-2">
                {categories.map((c) => (
                  <button
                    key={c.key}
                    type="button"
                    onClick={() => {
                      setSelectedCategory(c.key);
                      setManualUnit(c.defaultUnit);
                    }}
                    className={`py-2 px-3 rounded-xl text-xs font-bold border text-left flex items-center gap-2 ${
                      selectedCategory === c.key
                        ? 'border-emerald-600 bg-emerald-50 text-emerald-900'
                        : 'border-slate-200 bg-slate-50 text-slate-700'
                    }`}
                  >
                    <c.icon size={15} />
                    {c.labelBn}
                  </button>
                ))}
              </div>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                পরিমাণ ({manualUnit})
              </label>
              <input
                type="number"
                step="any"
                required
                value={manualQuantity}
                onChange={(e) => setManualQuantity(e.target.value)}
                placeholder="যেমন: ৩,৫০০"
                className="w-full px-3 py-2.5 rounded-xl border border-slate-300 text-base font-bold focus:outline-hidden focus:border-emerald-600"
              />
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                স্লিপ / চালান নম্বর (ঐচ্ছিক)
              </label>
              <input
                type="text"
                value={manualSlipNumber}
                onChange={(e) => setManualSlipNumber(e.target.value)}
                placeholder="যেমন: POCL-44912"
                className="w-full px-3 py-2 rounded-xl border border-slate-300 text-sm focus:outline-hidden focus:border-emerald-600"
              />
            </div>

            <button
              type="submit"
              disabled={isProcessing || !selectedCategory || !manualQuantity}
              className="w-full py-3 bg-amber-600 hover:bg-amber-700 text-white font-bold rounded-xl text-sm shadow-md transition disabled:opacity-50"
            >
              {isProcessing ? 'সংরক্ষণ হচ্ছে...' : 'সংরক্ষণ করুন'}
            </button>
          </form>
        </div>
      ) : (
        /* 4 Big High-Contrast Touch Cards */
        <main className="p-4">
          <p className="text-xs font-bold uppercase tracking-wider text-slate-500 mb-3">
            বিল বা চালান স্ক্যান করতে স্পর্শ করুন (২-ট্যাপ)
          </p>

          <div className="grid grid-cols-2 gap-3.5">
            {categories.map((cat) => {
              const Icon = cat.icon;
              const isSelected = selectedCategory === cat.key;

              return (
                <button
                  key={cat.key}
                  onClick={() => handleSelectCategory(cat.key)}
                  className={`group relative flex flex-col items-center justify-center p-5 rounded-2xl border-2 transition active:scale-95 shadow-sm min-h-[150px] ${
                    isSelected
                      ? `${cat.activeBorder} bg-emerald-50/50 shadow-md`
                      : 'border-slate-200 bg-white hover:border-slate-400'
                  }`}
                >
                  <div className={`w-14 h-14 rounded-2xl ${cat.color} text-white flex items-center justify-center mb-3 shadow-sm group-hover:scale-105 transition`}>
                    <Icon size={30} strokeWidth={2.3} />
                  </div>
                  <span className="text-base font-black text-slate-900 tracking-tight text-center leading-tight">
                    {cat.labelBn}
                  </span>
                  <span className="text-[11px] font-medium text-slate-500 text-center mt-1">
                    {cat.subBn}
                  </span>
                </button>
              );
            })}
          </div>
        </main>
      )}

      {/* "My Submissions" / আমার আপলোডসমূহ List */}
      <section className="px-4 mt-2">
        <div className="flex items-center justify-between mb-2">
          <h2 className="text-sm font-bold text-slate-800">আমার জমা দেওয়া চালানসমূহ</h2>
          <span className="text-xs font-semibold text-slate-500">
            মোট: {formatBanglaNumber(submissions.length)}
          </span>
        </div>

        {submissions.length === 0 ? (
          <div className="bg-white border border-slate-200 rounded-xl p-6 text-center text-slate-400 text-xs">
            এখনও কোনো চালান জমা দেওয়া হয়নি।
          </div>
        ) : (
          <div className="space-y-2">
            {submissions.slice(0, 10).map((sub) => {
              const isReceived = sub.status === 'received';
              const isUploading = sub.status === 'uploading';
              const isFailed = sub.status === 'failed';

              return (
                <div
                  key={sub.id}
                  className="bg-white border border-slate-200 rounded-xl p-3 flex items-center justify-between shadow-2xs"
                >
                  <div className="flex items-center gap-2.5">
                    <div className="w-8 h-8 rounded-lg bg-slate-100 flex items-center justify-center text-slate-700">
                      {sub.category === 'diesel' && <Fuel size={17} className="text-amber-600" />}
                      {sub.category === 'gas' && <Flame size={17} className="text-orange-600" />}
                      {sub.category === 'electricity' && <Zap size={17} className="text-yellow-600" />}
                      {sub.category === 'shipment' && <Truck size={17} className="text-blue-600" />}
                    </div>
                    <div>
                      <div className="flex items-center gap-1.5">
                        <span className="text-xs font-bold text-slate-900 font-mono">
                          {sub.receiptCode || sub.id.slice(0, 8).toUpperCase()}
                        </span>
                        <span className="text-[11px] text-slate-400">
                          {formatBanglaDate(sub.createdAt)}
                        </span>
                      </div>
                      <p className="text-[11px] text-slate-600 font-medium">
                        {sub.manualQuantity
                          ? `ম্যানুয়াল: ${formatBanglaNumber(sub.manualQuantity)} ${sub.manualUnit || ''}`
                          : sub.fileName || 'ক্যামেরা ফটো'}
                      </p>
                      {isFailed && sub.error && (
                        <p className="text-[10px] text-rose-500 font-medium truncate max-w-[180px]" title={sub.error}>
                          {sub.error}
                        </p>
                      )}
                    </div>
                  </div>

                  {/* Status Badges */}
                  <div>
                    {isReceived && (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-bold bg-emerald-100 text-emerald-800">
                        <CheckCircle2 size={12} /> গৃহীত
                      </span>
                    )}
                    {isUploading && (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-bold bg-sky-100 text-sky-800 animate-pulse">
                        <RefreshCw size={12} className="animate-spin" /> আপলোড
                      </span>
                    )}
                    {sub.status === 'queued' && (
                      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-bold bg-amber-100 text-amber-800">
                        <Clock size={12} /> অপেক্ষমান
                      </span>
                    )}
                    {isFailed && (
                      <div className="flex items-center gap-1.5">
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-[11px] font-bold bg-rose-100 text-rose-800">
                          <AlertCircle size={12} /> ব্যর্থ
                        </span>
                        <button
                          type="button"
                          onClick={() => handleRetry(sub.id)}
                          className="px-2 py-0.5 rounded-md text-[11px] font-bold bg-teal-600 hover:bg-teal-700 text-white flex items-center gap-1 shadow-2xs transition active:scale-95 cursor-pointer"
                          title="পুনরায় আপলোড চেষ্টা করুন"
                        >
                          <RefreshCw size={11} /> পুনরায় চেষ্টা
                        </button>
                      </div>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </section>
    </div>
  );
}

export default CaptureScreen;
