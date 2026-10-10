import React, { useState } from 'react';
import { ZoomIn, ZoomOut, RotateCw, Maximize2, FileText, Image as ImageIcon, Eye, AlertCircle } from 'lucide-react';
import { ReviewField } from './reviewApi';

interface DocumentImageViewerProps {
  documentId?: string;
  fileName: string;
  contentType?: string;
  activeField: ReviewField | null;
  fields: ReviewField[];
}

export function DocumentImageViewer({
  documentId,
  fileName,
  contentType,
  activeField,
  fields,
}: DocumentImageViewerProps) {
  const [zoom, setZoom] = useState<number>(1);
  const [rotation, setRotation] = useState<number>(0);
  const [viewMode, setViewMode] = useState<'image' | 'data'>('image');
  const [imageError, setImageError] = useState<boolean>(false);

  function handleZoomIn() {
    setZoom((prev) => Math.min(prev + 0.25, 3.0));
  }

  function handleZoomOut() {
    setZoom((prev) => Math.max(prev - 0.25, 0.4));
  }

  function handleRotate() {
    setRotation((prev) => (prev + 90) % 360);
  }

  function handleReset() {
    setZoom(1);
    setRotation(0);
  }

  React.useEffect(() => {
    setImageError(false);
    setZoom(1);
    setRotation(0);
  }, [documentId]);

  // Parse bounding box if present
  let activeBbox: { left: number; top: number; width: number; height: number } | null = null;
  if (activeField?.boundingBoxJson) {
    try {
      activeBbox = JSON.parse(activeField.boundingBoxJson);
    } catch {
      // Ignored
    }
  }

  const fileUrl = documentId ? `/api/v1/documents/${documentId}/file` : null;

  const findFieldVal = (name: string) => {
    const f = fields.find(
      (item) => item.fieldName?.trim().toLowerCase() === name.trim().toLowerCase()
    );
    return f?.correctedValue || f?.normalizedValue || f?.rawValue;
  };

  const vendorName =
    findFieldVal('Vendor') ||
    findFieldVal('Organization') ||
    findFieldVal('Provider') ||
    'ইউটিলিটি চালান (Utility Provider)';
  const billNo =
    findFieldVal('BillNumber') ||
    findFieldVal('BillNo') ||
    findFieldVal('AccountNumber') ||
    'N/A';
  const period =
    findFieldVal('BillingPeriod') ||
    findFieldVal('BillMonth') ||
    findFieldVal('Period') ||
    'চলতি মাস';
  const quantity = findFieldVal('Quantity') || '০';
  const unit = findFieldVal('Unit') || '';
  const amount =
    findFieldVal('AmountBdt') ||
    findFieldVal('Amount') ||
    findFieldVal('TotalAmount') ||
    '০.০০';

  return (
    <div className="flex flex-col h-full bg-slate-900 rounded-2xl overflow-hidden border border-slate-800 shadow-md">
      {/* Control Toolbar */}
      <div className="flex items-center justify-between px-4 py-2.5 bg-slate-950/90 border-b border-slate-800 text-slate-300 text-xs shrink-0">
        <div className="flex items-center gap-2 truncate max-w-[200px] lg:max-w-xs">
          <FileText size={16} className="text-emerald-400 shrink-0" />
          <span className="font-mono font-medium truncate text-white" title={fileName}>
            {fileName}
          </span>
        </div>

        {/* View Mode Switcher */}
        <div className="flex items-center bg-slate-900 border border-slate-700/80 rounded-lg p-0.5">
          <button
            onClick={() => setViewMode('image')}
            className={`flex items-center gap-1 px-2 py-1 rounded text-[11px] font-bold transition ${
              viewMode === 'image'
                ? 'bg-emerald-600 text-white shadow-xs'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <ImageIcon size={13} />
            <span>মূল ছবি</span>
          </button>
          <button
            onClick={() => setViewMode('data')}
            className={`flex items-center gap-1 px-2 py-1 rounded text-[11px] font-bold transition ${
              viewMode === 'data'
                ? 'bg-emerald-600 text-white shadow-xs'
                : 'text-slate-400 hover:text-white'
            }`}
          >
            <Eye size={13} />
            <span>ডিজিটাল ডেটা</span>
          </button>
        </div>

        {/* Zoom & Rotation Controls */}
        <div className="flex items-center gap-1">
          <button
            onClick={handleZoomOut}
            title="ছোট করুন (Zoom Out)"
            className="p-1.5 hover:bg-slate-800 rounded-lg text-slate-400 hover:text-white transition"
          >
            <ZoomOut size={15} />
          </button>
          <span className="font-mono text-[11px] px-1 text-slate-400 w-10 text-center">
            {Math.round(zoom * 100)}%
          </span>
          <button
            onClick={handleZoomIn}
            title="বড় করুন (Zoom In)"
            className="p-1.5 hover:bg-slate-800 rounded-lg text-slate-400 hover:text-white transition"
          >
            <ZoomIn size={15} />
          </button>
          <button
            onClick={handleRotate}
            title="ঘুরান (Rotate 90°)"
            className="p-1.5 hover:bg-slate-800 rounded-lg text-slate-400 hover:text-white transition"
          >
            <RotateCw size={15} />
          </button>
          <button
            onClick={handleReset}
            title="রিসেট ফিট (Reset)"
            className="p-1.5 hover:bg-slate-800 rounded-lg text-slate-400 hover:text-white transition"
          >
            <Maximize2 size={15} />
          </button>
        </div>
      </div>

      {/* Canvas Area */}
      <div className="flex-1 relative overflow-auto p-4 flex items-center justify-center min-h-[460px] bg-slate-950">
        <div
          style={{
            transform: `scale(${zoom}) rotate(${rotation}deg)`,
            transformOrigin: 'center center',
            transition: 'transform 0.15s ease-out',
          }}
          className="relative max-w-full shadow-2xl rounded-lg overflow-hidden flex items-center justify-center"
        >
          {viewMode === 'image' && fileUrl && !imageError ? (
            <div className="relative inline-block max-w-full">
              <img
                src={fileUrl}
                alt={fileName}
                onError={() => setImageError(true)}
                className="max-h-[640px] w-auto object-contain rounded-lg shadow-2xl select-none pointer-events-auto bg-slate-900"
              />

              {/* Bounding Box Highlight Overlay */}
              {activeBbox && (
                <div
                  style={{
                    position: 'absolute',
                    left: `${activeBbox.left * 100}%`,
                    top: `${activeBbox.top * 100}%`,
                    width: `${activeBbox.width * 100}%`,
                    height: `${activeBbox.height * 100}%`,
                  }}
                  className="border-3 border-amber-500 bg-amber-400/25 rounded-md pointer-events-none animate-pulse shadow-lg transition-all"
                >
                  <span className="absolute -top-5 left-0 bg-amber-600 text-white font-bold text-[10px] px-1.5 py-0.5 rounded shadow whitespace-nowrap">
                    {activeField?.fieldName}
                  </span>
                </div>
              )}
            </div>
          ) : (
            /* Digital Structured Data Preview Card */
            <div className="w-[520px] min-h-[600px] bg-white p-6 text-slate-900 font-sans text-xs select-none rounded-xl border border-slate-200 shadow-xl">
              {/* Header */}
              <div className="border-b-2 border-slate-200 pb-4 mb-4">
                <div className="flex justify-between items-start">
                  <div>
                    <h2 className="text-base font-black text-emerald-800 tracking-tight">
                      {vendorName}
                    </h2>
                    <p className="text-[11px] text-slate-500">কার্বনবিল ডিজিটাল চালান প্রিভিউ</p>
                  </div>
                  <div className="text-right">
                    <span className="font-mono text-xs font-bold text-slate-700">
                      বিল নং: {billNo}
                    </span>
                    <p className="text-[10px] text-slate-500 font-medium">
                      হিসাবের মাস: {period}
                    </p>
                  </div>
                </div>
              </div>

              {/* Document Metadata Table */}
              <div className="grid grid-cols-2 gap-3 mb-5 bg-slate-50 p-3 rounded-xl border border-slate-200">
                <div>
                  <p className="text-[10px] uppercase text-slate-400 font-bold">ফাইল নাম</p>
                  <p className="font-bold text-slate-800 truncate font-mono text-[11px]" title={fileName}>
                    {fileName}
                  </p>
                </div>
                <div>
                  <p className="text-[10px] uppercase text-slate-400 font-bold">ডকুমেন্ট আইডি</p>
                  <p className="font-mono text-[11px] text-slate-600 truncate">
                    {documentId ? documentId.slice(0, 8).toUpperCase() : 'N/A'}
                  </p>
                </div>
              </div>

              {/* Extracted Values Table */}
              <div className="bg-slate-50 border-2 border-slate-200 rounded-xl p-4 mb-4 space-y-3">
                <div className="flex justify-between items-center py-1.5 border-b border-slate-200">
                  <span className="text-slate-600 font-medium">রেকর্ডকৃত মোট ব্যবহার (Consumption):</span>
                  <span className="font-mono font-bold text-slate-900 text-sm">
                    {quantity} {unit}
                  </span>
                </div>

                <div className="flex justify-between items-center pt-2">
                  <span className="text-slate-800 font-black text-sm">সর্বমোট প্রদেয় টাকা (Net Amount):</span>
                  <span className="font-mono font-black text-emerald-700 text-base">
                    ৳ {amount}
                  </span>
                </div>
              </div>

              {/* All Extracted OCR Fields Grid */}
              <div className="mt-4 pt-3 border-t border-slate-100">
                <p className="text-[11px] font-bold text-slate-600 mb-2">উদ্ধারকৃত ফিল্ড তালিকা ({fields.length}টি ফিল্ড):</p>
                <div className="space-y-1.5 max-h-48 overflow-y-auto pr-1">
                  {fields.map((f) => (
                    <div
                      key={f.id}
                      className={`flex justify-between items-center p-2 rounded-lg text-[11px] ${
                        activeField?.id === f.id
                          ? 'bg-emerald-50 border border-emerald-300 font-bold text-emerald-950'
                          : 'bg-white border border-slate-100 text-slate-700'
                      }`}
                    >
                      <span className="font-semibold">{f.fieldName}</span>
                      <span className="font-mono">{f.correctedValue || f.normalizedValue || f.rawValue}</span>
                    </div>
                  ))}
                </div>
              </div>

              {/* Status Stamp */}
              <div className="mt-6 flex justify-between items-center pt-3 border-t border-slate-100">
                <span className="text-[10px] text-slate-400 font-medium">
                  {imageError ? 'আসল ইমেজ ফাইল না থাকায় ডিজিটাল কার্ড দেখানো হচ্ছে' : 'ডিজিটাল যাচাইযোগ্য কপি'}
                </span>
                <div className="border-2 border-emerald-600 text-emerald-800 font-bold text-[10px] uppercase px-3 py-1 rounded-lg inline-block">
                  Verified Ingestion
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export default DocumentImageViewer;
