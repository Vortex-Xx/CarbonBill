import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

const resources = {
  en: {
    translation: {
      appName: 'CarbonBill',
      tagline: 'Defensible SME Carbon Accounting & Buyer Assurance',
      roles: {
        FloorStaff: 'Floor Staff',
        Accountant: 'Accountant',
        Compliance: 'Compliance Officer',
        Owner: 'Factory Owner',
        Consultant: 'Consultant',
        PlatformAdmin: 'Platform Admin'
      },
      nav: {
        dashboard: 'Dashboard',
        floorCapture: 'Floor Capture',
        reviewQueue: 'Review Queue',
        reports: 'Reports',
        auditorPortal: 'Auditor Portal',
        logout: 'Sign Out'
      },
      login: {
        title: 'Sign In',
        emailPlaceholder: 'Email Address or Phone',
        passwordPlaceholder: 'Password',
        submit: 'Sign In',
        signingIn: 'Signing in...',
        registerOrg: 'Register Organization',
        joinOrg: 'Join Organization',
        orgName: 'Organization Name',
        fullName: 'Full Name',
        facilityType: 'Facility Type',
        createOrgSubmit: 'Create Organization',
        creatingOrg: 'Creating organization...',
        inviteToken: 'Invitation Token',
        acceptInvite: 'Accept Invitation',
        joinSubmit: 'Join Team',
        loginFailed: 'Authentication failed. Please verify your email/phone and password.'
      },
      floor: {
        title: 'Capture Bill & Challan',
        subtitle: 'Quick 2-tap photo submission for bills, fuel receipts, and delivery challans',
        categories: {
          diesel: 'Diesel & Fuel',
          gas: 'Gas Bill',
          electricity: 'Electricity Bill',
          shipment: 'Delivery Challan',
          other: 'General Receipt'
        },
        tapToCapture: 'Tap to Take Photo',
        manualFallback: 'Dark or damaged receipt? Enter values manually',
        quantityLabel: 'Quantity (Litres / kWh / m³ / BDT)',
        slipNumber: 'Slip / Bill Number',
        submitReceipt: 'Submit Receipt',
        submitting: 'Submitting receipt...',
        recentSubmissions: 'My Recent Submissions',
        receiptConfirmed: 'Receipt successfully confirmed! ID:',
        offlineNotice: 'You are currently offline. Submissions are saved locally and will upload automatically once reconnected.',
        batchMode: 'Batch Mode',
        cameraReady: 'Camera Ready',
        uploadFile: 'Select File from Device',
        dropzoneText: 'Drag & drop bill image, or click to browse'
      },
      review: {
        title: 'Review & Verification Workspace',
        subtitle: 'Dual-pane verification workspace for accounts & compliance',
        filterAll: 'All',
        filterGas: 'Gas',
        filterElectricity: 'Electricity',
        filterDiesel: 'Diesel',
        filterChallan: 'Challan',
        filterOther: 'Other',
        emptyQueue: 'All documents reviewed! 🎉',
        noDocSelected: 'No document selected.',
        confirmButton: 'Confirm & Save (Enter)',
        confirming: 'Confirming...',
        nextButton: 'Next (N)',
        retakeButton: 'Request Photo Retake from Floor',
        originalImage: 'Original Image',
        digitalData: 'Extracted Data',
        confidence: 'Confidence',
        consumption: 'Consumption',
        totalAmount: 'Total Amount',
        rawOcr: 'Raw OCR',
        isEstimated: 'Estimated Data / Proxy Calculation',
        bulkApprove: 'Bulk Approve High Confidence'
      },
      dashboard: {
        title: 'Carbon Dashboard',
        scope1: 'Scope 1 (Direct Fuels & Gas)',
        scope2: 'Scope 2 (Grid Electricity)',
        scope3: 'Scope 3 (Freight & Transport)',
        dataQualityScore: 'Data Quality Score (DQS)',
        topFlags: 'Attention Required / Carbon Flags',
        topActions: 'Top Financial Savings in BDT',
        missingDocs: 'Expected Documents Checklist',
        verifiedLabel: 'Verified',
        estimatedLabel: 'Estimated'
      },
      auditor: {
        title: 'Auditor Verification Portal',
        subtitle: 'Cryptographically sealed GHG accounting ledger & provenance chain',
        totalEmissions: 'Total Footprint',
        dqsGrade: 'Audit Preparedness Grade',
        methodologyGuide: 'Scopes & Grading Guide',
        exportCsv: 'Export Ledger (CSV)',
        searchPlaceholder: 'Search by provider, bill number, division, or document ID...',
        provenanceInspector: 'Provenance Inspector',
        hashStamp: 'Cryptographic Hash Stamp (SHA-256)'
      }
    }
  },
  bn: {
    translation: {
      appName: 'কার্বনবিল',
      tagline: 'সহজ ও নির্ভুল এসএমই কার্বন অ্যাকাউন্টিং ও আন্তর্জাতিক ক্রেতা অডিট',
      roles: {
        FloorStaff: 'ফ্লোর স্টাফ',
        Accountant: 'হিসাবরক্ষক',
        Compliance: 'কমপ্লায়েন্স অফিসার',
        Owner: 'মালিক / পরিচালক',
        Consultant: 'কনসালট্যান্ট',
        PlatformAdmin: 'প্ল্যাটফর্ম অ্যাডমিন'
      },
      nav: {
        dashboard: 'ড্যাশবোর্ড',
        floorCapture: 'বিল আপলোড (ফ্লোর)',
        reviewQueue: 'রিভিউ কিউ',
        reports: 'রিপোর্ট',
        auditorPortal: 'অডিটর পোর্টাল',
        logout: 'লগআউট'
      },
      login: {
        title: 'লগইন করুন',
        emailPlaceholder: 'ইমেইল এড্রেস বা মোবাইল নম্বর',
        passwordPlaceholder: 'পাসওয়ার্ড',
        submit: 'লগইন',
        signingIn: 'প্রবেশ করা হচ্ছে...',
        registerOrg: 'নতুন প্রতিষ্ঠান',
        joinOrg: 'আমন্ত্রণ গ্রহণ',
        orgName: 'প্রতিষ্ঠানের নাম',
        fullName: 'আপনার পূর্ণ নাম',
        facilityType: 'কারখানার ধরণ',
        createOrgSubmit: 'প্রতিষ্ঠান শুরু করুন',
        creatingOrg: 'নিবন্ধন করা হচ্ছে...',
        inviteToken: 'আমন্ত্রণ টোকেন',
        acceptInvite: 'আমন্ত্রণ নিশ্চিত করুন',
        joinSubmit: 'দলে যোগ দিন',
        loginFailed: 'লগইন ব্যর্থ হয়েছে। ইমেইল বা পাসওয়ার্ড সঠিক কি না যাচাই করুন।'
      },
      floor: {
        title: 'রসিদ ও বিল আপলোড',
        subtitle: 'সহজ দুই-ট্যাপে আপনার ইউটিলিটি বিল বা ডিজেল রসিদের ছবি তুলুন',
        categories: {
          diesel: 'ডিজেল / জ্বালানি',
          gas: 'গ্যাস বিল',
          electricity: 'বিদ্যুৎ বিল',
          shipment: 'চালান / পরিবহন',
          other: 'সাধারণ রসিদ'
        },
        tapToCapture: 'ছবি তুলতে ট্যাপ করুন',
        manualFallback: 'ছবি স্পষ্ট না হলে? সরাসরি সংখ্যা লিখুন',
        quantityLabel: 'পরিমাণ (লিটার / ইউনিট / ঘনমিটার / টাকা)',
        slipNumber: 'রসিদ / স্লিপ নম্বর',
        submitReceipt: 'রসিদ জমা দিন',
        submitting: 'জমা দেওয়া হচ্ছে...',
        recentSubmissions: 'আমার সাম্প্রতিক আপলোড',
        receiptConfirmed: 'রসিদ সফলভাবে গৃহীত হয়েছে! আইডি:',
        offlineNotice: 'আপনি অফলাইনে আছেন। ইন্টারনেট সংযুক্ত হলে স্বয়ংক্রিয়ভাবে জমা হবে।',
        batchMode: 'ব্যাচ মোড',
        cameraReady: 'ক্যামেরা প্রস্তুত',
        uploadFile: 'ডিভাইস থেকে ফাইল নির্বাচন করুন',
        dropzoneText: 'বিলের ছবি এখানে ড্র্যাগ ও ড্রপ করুন, অথবা ব্রাউজ করতে ক্লিক করুন'
      },
      review: {
        title: 'চালান ও বিল পর্যালোচনা ওয়ার্কস্পেস',
        subtitle: 'হিসাবরক্ষক ও কমপ্লায়েন্সের জন্য অপটিমাইজড ডুয়াল ভিউ',
        filterAll: 'সবগুলো',
        filterGas: 'গ্যাস',
        filterElectricity: 'বিদ্যুৎ',
        filterDiesel: 'ডিজেল',
        filterChallan: 'চালান',
        filterOther: 'অন্যান্য',
        emptyQueue: 'কিউতে কোনো বিল বাকি নেই! 🎉',
        noDocSelected: 'কোনো ডকুমেন্ট নির্বাচিত নেই।',
        confirmButton: 'অনুমোদন করুন (Enter)',
        confirming: 'অনুমোদন হচ্ছে...',
        nextButton: 'পরবর্তী (N)',
        retakeButton: 'ফ্লোরে পুনরায় তোলার অনুরোধ পাঠান',
        originalImage: 'মূল ছবি',
        digitalData: 'ডিজিটাল ডেটা',
        confidence: 'আত্মবিশ্বাস',
        consumption: 'ব্যবহার',
        totalAmount: 'মোট টাকা',
        rawOcr: 'মূল ও সি আর',
        isEstimated: 'অনুপস্থিত মাসের আনুমানিক হিসাব',
        bulkApprove: 'উচ্চ আত্মবিশ্বাসী বিলসমূহ একযোগে অনুমোদন'
      },
      dashboard: {
        title: 'কার্বন ড্যাশবোর্ড',
        scope1: 'স্কোপ ১ (প্রত্যক্ষ জ্বালানি ও গ্যাস দহন)',
        scope2: 'স্কোপ ২ (ক্রয়কৃত গ্রিড বিদ্যুৎ)',
        scope3: 'স্কোপ ৩ (সরবরাহ চেইন ও পরিবহন)',
        dataQualityScore: 'ডেটা কোয়ালিটি স্কোর (DQS)',
        topFlags: 'জরুরি নোটিশ / কার্বন ফ্ল্যাগ',
        topActions: 'টাকা সাশ্রয়ী পদক্ষেপ (MACC)',
        missingDocs: 'অনুপস্থিত বিল তালিকা',
        verifiedLabel: 'যাচাইকৃত (Verified)',
        estimatedLabel: 'আনুমানিক (Estimated)'
      },
      auditor: {
        title: 'অডিটর পোর্টাল',
        subtitle: 'ডিজিটাল ক্রিপ্টোগ্রাফিক সিলযুক্ত GHG লেজার ও প্রমাণ শৃঙ্খল',
        totalEmissions: 'মোট নির্গমন (Total)',
        dqsGrade: 'অডিট প্রস্তুতি গ্রেড',
        methodologyGuide: 'স্কোপ ও গ্রেড গাইডলাইন',
        exportCsv: 'লেজার এক্সপোর্ট (CSV)',
        searchPlaceholder: 'সরবরাহকারী, বিল নম্বর, বিভাগ বা ডকুমেন্ট আইডি দিয়ে খুঁজুন...',
        provenanceInspector: 'প্রমাণ পরিদর্শক',
        hashStamp: 'ক্রিপ্টোগ্রাফিক হ্যাশ স্ট্যাম্প (SHA-256)'
      }
    }
  }
};

// Default language: inspect localStorage for persisted preference, otherwise default to English
const initialLanguage =
  (typeof window !== 'undefined' && localStorage.getItem('carbonbill_lang')) || 'en';

i18n
  .use(initReactI18next)
  .init({
    resources,
    lng: initialLanguage,
    fallbackLng: 'en',
    interpolation: {
      escapeValue: false
    }
  });

// Persist user language choice in browser localStorage
if (typeof window !== 'undefined') {
  i18n.on('languageChanged', (lng: string) => {
    try {
      localStorage.setItem('carbonbill_lang', lng);
    } catch {
      // Storage unavailable or private browsing quota restriction
    }
  });
}

export default i18n;
