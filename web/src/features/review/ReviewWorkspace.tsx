import React, { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import {
  fetchReviewQueue,
  confirmDocument,
  bulkConfirmDocuments,
  fetchReviewSettings,
  ReviewQueueItem,
  ReviewField,
  ConfirmRequest,
} from './reviewApi';
import { DocumentImageViewer } from './DocumentImageViewer';
import { ReviewFieldsForm } from './ReviewFieldsForm';
import { ReviewQueueSidebar } from './ReviewQueueSidebar';
import { ArrowLeft, RefreshCw, CheckCircle2, AlertTriangle, Languages } from 'lucide-react';

interface ReviewWorkspaceProps {
  onBack?: () => void;
}

export function ReviewWorkspace({ onBack }: ReviewWorkspaceProps) {
  const { t, i18n } = useTranslation();
  const isBangla = i18n.language === 'bn';

  const [queue, setQueue] = useState<ReviewQueueItem[]>([]);
  const [selectedDocId, setSelectedDocId] = useState<string | null>(null);
  const [activeField, setActiveField] = useState<ReviewField | null>(null);
  const [filterType, setFilterType] = useState<string>('');
  const [isBulkEnabled, setIsBulkEnabled] = useState<boolean>(false);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [bannerMessage, setBannerMessage] = useState<string | null>(null);

  const loadData = useCallback(async () => {
    setIsLoading(true);
    try {
      const [items, settings] = await Promise.all([
        fetchReviewQueue(filterType),
        fetchReviewSettings(),
      ]);
      setQueue(items);
      setIsBulkEnabled(settings.autoConfirmEnabled || true);

      // Select target docId from query params if specified
      const params = new URLSearchParams(window.location.search);
      const targetDocId = params.get('docId');
      if (targetDocId && items.some((d) => d.documentId.toLowerCase() === targetDocId.toLowerCase())) {
        setSelectedDocId(targetDocId);
      } else if (!selectedDocId || !items.some((d) => d.documentId === selectedDocId)) {
        // Prioritize the newest document that has extracted fields
        const withFields = items.filter((d) => d.fields && d.fields.length > 0);
        if (withFields.length > 0) {
          const newest = [...withFields].sort(
            (a, b) => new Date(b.capturedAtUtc).getTime() - new Date(a.capturedAtUtc).getTime()
          )[0];
          setSelectedDocId(newest.documentId);
        } else if (items.length > 0) {
          setSelectedDocId(items[0].documentId);
        }
      }
    } finally {
      setIsLoading(false);
    }
  }, [filterType, selectedDocId]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const currentDoc: ReviewQueueItem | undefined =
    queue.find((d) => d.documentId === selectedDocId) || queue[0];

  const handleNextDocument = useCallback(() => {
    if (queue.length > 0) {
      const currIdx = queue.findIndex((d) => d.documentId === currentDoc?.documentId);
      const nextIdx = currIdx === -1 ? 0 : (currIdx + 1) % queue.length;
      setSelectedDocId(queue[nextIdx].documentId);
      setActiveField(null);
    }
  }, [queue, currentDoc?.documentId]);

  const handleConfirmCurrent = useCallback(
    async (payload: ConfirmRequest) => {
      if (!currentDoc) return;

      const success = await confirmDocument(currentDoc.documentId, payload);
      if (success) {
        setBannerMessage(
          isBangla
            ? `চালান ${currentDoc.fileName} সফলভাবে অনুমোদিত হয়েছে!`
            : `Document ${currentDoc.fileName} approved successfully!`
        );
        setTimeout(() => setBannerMessage(null), 3000);

        const currId = currentDoc.documentId;
        setQueue((prev) => prev.filter((d) => d.documentId !== currId));
        setSelectedDocId(null);
        setActiveField(null);
      }
    },
    [currentDoc, isBangla]
  );

  const handleRejectRetake = useCallback(
    async (reason: string) => {
      if (!currentDoc) return;
      setBannerMessage(
        isBangla
          ? `ফ্লোরে ছবি পুনরায় তোলার অনুরোধ পাঠানো হয়েছে (${reason})`
          : `Retake requested from floor (${reason})`
      );
      setTimeout(() => setBannerMessage(null), 3000);
      handleNextDocument();
    },
    [currentDoc, handleNextDocument, isBangla]
  );

  const handleBulkConfirm = useCallback(async () => {
    const highConfIds = queue
      .filter((d) => d.overallConfidence >= 0.9)
      .map((d) => d.documentId);

    if (highConfIds.length === 0) return;

    const res = await bulkConfirmDocuments(highConfIds);
    setBannerMessage(
      isBangla
        ? `${res.succeeded}টি উচ্চ আত্মবিশ্বাসী চালান একযোগে অনুমোদিত হয়েছে!`
        : `${res.succeeded} high-confidence documents approved in bulk!`
    );
    setTimeout(() => setBannerMessage(null), 3000);
    loadData();
  }, [queue, loadData, isBangla]);

  // Global Keyboard Shortcuts (N for next, Enter handled in form)
  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent) {
      if (
        document.activeElement?.tagName === 'INPUT' ||
        document.activeElement?.tagName === 'TEXTAREA'
      ) {
        return; // Don't trigger shortcuts when typing in inputs
      }

      if (e.key === 'n' || e.key === 'N') {
        e.preventDefault();
        handleNextDocument();
      }
    }

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [handleNextDocument]);

  return (
    <div className="flex flex-col h-screen bg-slate-100 text-slate-900 font-sans overflow-hidden">
      {/* Workspace Top Header */}
      <header className="h-14 bg-white border-b border-slate-200 px-6 flex items-center justify-between shadow-xs shrink-0 z-10">
        <div className="flex items-center gap-3">
          {onBack && (
            <button
              onClick={onBack}
              className="p-1.5 -ml-1 text-slate-600 hover:bg-slate-100 rounded-lg transition"
              title={isBangla ? 'ফিরে যান' : 'Go Back'}
            >
              <ArrowLeft size={18} />
            </button>
          )}
          <div>
            <h1 className="text-base font-black text-slate-900 tracking-tight">
              {isBangla
                ? 'চালান ও বিল পর্যালোচনা ওয়ার্কস্পেস'
                : 'Review & Verification Workspace'}
            </h1>
            <p className="text-[11px] text-slate-500 font-medium">
              {isBangla
                ? 'হিসাবরক্ষক ও কমপ্লায়েন্সের জন্য অপটিমাইজড ডুয়াল ভিউ'
                : 'Dual-pane verification workspace for accounts & compliance'}
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {bannerMessage && (
            <div className="bg-emerald-50 border border-emerald-300 text-emerald-800 text-xs font-bold px-3 py-1.5 rounded-xl flex items-center gap-1.5 animate-fade-in shadow-xs">
              <CheckCircle2 size={15} className="text-emerald-600" />
              {bannerMessage}
            </div>
          )}

          {/* Language Switcher */}
          <button
            onClick={() => i18n.changeLanguage(isBangla ? 'en' : 'bn')}
            className="text-xs font-semibold px-2.5 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg border border-slate-200 transition"
            title={isBangla ? 'Switch to English' : 'বাংলায় পরিবর্তন করুন'}
          >
            {isBangla ? 'English' : 'বাংলা'}
          </button>

          <button
            onClick={loadData}
            title={isBangla ? 'রিফ্রেশ করুন' : 'Refresh Queue'}
            className="p-2 text-slate-600 hover:bg-slate-100 rounded-xl transition"
          >
            <RefreshCw size={16} className={isLoading ? 'animate-spin text-emerald-600' : ''} />
          </button>
        </div>
      </header>

      {/* Main 3-Column Review Layout */}
      <div className="flex-1 grid grid-cols-12 gap-4 p-4 overflow-hidden">
        {/* Leftmost Column: Review Queue (3 cols) */}
        <div className="col-span-12 lg:col-span-3 h-full overflow-hidden">
          <ReviewQueueSidebar
            queue={queue}
            selectedDocumentId={currentDoc?.documentId || null}
            onSelectDocument={(doc) => {
              setSelectedDocId(doc.documentId);
              setActiveField(null);
            }}
            selectedFilter={filterType}
            onFilterChange={(filter) => {
              setFilterType(filter);
              setSelectedDocId(null);
              setActiveField(null);
            }}
            onBulkConfirm={handleBulkConfirm}
            isBulkEnabled={isBulkEnabled}
            isBangla={isBangla}
          />
        </div>

        {/* Center Column: Original Document Image Viewer (5 cols) */}
        <div className="col-span-12 lg:col-span-5 h-full overflow-hidden">
          {currentDoc ? (
            <DocumentImageViewer
              documentId={currentDoc.documentId}
              fileName={currentDoc.fileName}
              contentType={currentDoc.contentType}
              activeField={activeField}
              fields={currentDoc.fields}
              isBangla={isBangla}
            />
          ) : (
            <div className="flex items-center justify-center h-full bg-white rounded-2xl border border-slate-200 p-8 text-center text-slate-400 text-sm">
              {isBangla ? 'কোনো ডকুমেন্ট নির্বাচিত নেই।' : 'No document selected.'}
            </div>
          )}
        </div>

        {/* Right Column: Extracted Fields Editor (4 cols) */}
        <div className="col-span-12 lg:col-span-4 h-full overflow-hidden">
          {currentDoc ? (
            <ReviewFieldsForm
              document={currentDoc}
              activeField={activeField}
              onSelectField={setActiveField}
              onConfirm={handleConfirmCurrent}
              onRejectRetake={handleRejectRetake}
              onNextDocument={handleNextDocument}
              isBangla={isBangla}
            />
          ) : (
            <div className="flex items-center justify-center h-full bg-white rounded-2xl border border-slate-200 p-8 text-center text-slate-400 text-sm">
              {isBangla ? 'কিউ খালি।' : 'Review queue is empty.'}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

export default ReviewWorkspace;
