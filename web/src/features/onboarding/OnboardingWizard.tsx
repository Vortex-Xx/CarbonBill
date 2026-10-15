import React, { useState, useEffect } from 'react';
import { apiClient } from '../../lib/apiClient';

interface SiteDto {
  id: string;
  name: string;
  address?: string;
  city?: string;
  isPrimary: boolean;
  assetsCount: number;
}

export const OnboardingWizard: React.FC = () => {
  const [step, setStep] = useState<1 | 2 | 3>(1);
  const [sites, setSites] = useState<SiteDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // Step 1: Site
  const [siteName, setSiteName] = useState('Savar Export Processing Unit');
  const [address, setAddress] = useState('Plot 12-14, DEPZ, Savar');
  const [city, setCity] = useState('Dhaka');

  // Step 2: Assets & Facility Profile
  const [hasBoiler, setHasBoiler] = useState(true);
  const [hasGenset, setHasGenset] = useState(true);
  const [roofAreaSqFt, setRoofAreaSqFt] = useState(35000);
  const [buildingOwnership, setBuildingOwnership] = useState('Owned');
  const [annualPieces, setAnnualPieces] = useState(1200000);

  useEffect(() => {
    loadSites();
  }, []);

  const loadSites = async () => {
    try {
      const data = await apiClient.get<SiteDto[]>('/api/v1/onboarding/sites');
      setSites(data);
    } catch {
      // Ignore if new organization
    }
  };

  const handleApplyRmgTemplate = async () => {
    setLoading(true);
    setErrorMsg(null);
    try {
      await apiClient.post('/api/v1/onboarding/templates/rmg');
      setSuccessMsg('পোশাক কারখানা (RMG) স্ট্যান্ডার্ড টেমপ্লেট সফলভাবে যুক্ত হয়েছে!');
      await loadSites();
      setStep(3);
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'টেমপ্লেট তৈরি ব্যর্থ হয়েছে।');
    } finally {
      setLoading(false);
    }
  };

  const handleSaveProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setErrorMsg(null);

    try {
      await apiClient.post('/api/v1/onboarding/profile/facility', {
        hasBoiler,
        hasGenset,
        roofAreaSqFt,
        buildingOwnership,
        budgetBandBdt: '1000000-5000000',
        annualProductionVolume: annualPieces,
        productionUnit: 'piece',
      });

      setSuccessMsg('কারখানার প্রোফাইল ও এনার্জি বিবরণ সংরক্ষিত হয়েছে!');
      setStep(3);
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'সংরক্ষণ ব্যর্থ হয়েছে।');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ maxWidth: '800px', margin: '2rem auto', padding: '1.5rem', fontFamily: 'system-ui, sans-serif', color: '#1e293b' }}>
      <div style={{ marginBottom: '2rem', textAlign: 'center' }}>
        <h1 style={{ fontSize: '1.75rem', fontWeight: 'bold', margin: '0 0 0.5rem 0', color: '#0f172a' }}>
          কারখানা অনবোর্ডিং ও সেটআপ (Onboarding Setup)
        </h1>
        <p style={{ color: '#64748b', fontSize: '0.95rem', margin: 0 }}>
          আপনার কারখানার বিদ্যুৎ মিটার, জেনারেটর, বয়লার এবং মাসিক বিল রুল ২ মিনিটে কনফিগার করুন
        </p>
      </div>

      {/* Step Indicators */}
      <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '2rem', borderBottom: '2px solid #e2e8f0', paddingBottom: '1rem' }}>
        <div style={{ fontWeight: step === 1 ? 'bold' : 'normal', color: step === 1 ? '#10b981' : '#64748b' }}>
          ১. সাইট ও লোকেশন (Site)
        </div>
        <div style={{ fontWeight: step === 2 ? 'bold' : 'normal', color: step === 2 ? '#10b981' : '#64748b' }}>
          ২. যন্ত্রপাতি ও প্রোফাইল (Assets)
        </div>
        <div style={{ fontWeight: step === 3 ? 'bold' : 'normal', color: step === 3 ? '#10b981' : '#64748b' }}>
          ৩. বিল জমা শিডিউল (Calendar)
        </div>
      </div>

      {successMsg && (
        <div style={{ padding: '0.75rem 1rem', borderRadius: '0.5rem', backgroundColor: '#ecfdf5', border: '1px solid #10b981', color: '#065f46', marginBottom: '1.5rem', fontSize: '0.875rem' }}>
          {successMsg}
        </div>
      )}

      {errorMsg && (
        <div style={{ padding: '0.75rem 1rem', borderRadius: '0.5rem', backgroundColor: '#fef2f2', border: '1px solid #ef4444', color: '#991b1b', marginBottom: '1.5rem', fontSize: '0.875rem' }}>
          {errorMsg}
        </div>
      )}

      {step === 1 && (
        <div style={{ backgroundColor: '#fff', padding: '2rem', borderRadius: '0.75rem', border: '1px solid #e2e8f0', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.05)' }}>
          <h2 style={{ fontSize: '1.25rem', fontWeight: 600, marginBottom: '1rem' }}>কারখানার প্রাথমিক সাইট যুক্ত করুন</h2>
          <div style={{ display: 'grid', gap: '1rem', marginBottom: '1.5rem' }}>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, marginBottom: '0.25rem' }}>সাইটের নাম</label>
              <input
                type="text"
                value={siteName}
                onChange={(e) => setSiteName(e.target.value)}
                style={{ width: '100%', boxSizing: 'border-box', padding: '0.5rem 0.75rem', borderRadius: '0.375rem', border: '1px solid #cbd5e1' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, marginBottom: '0.25rem' }}>ঠিকানা</label>
              <input
                type="text"
                value={address}
                onChange={(e) => setAddress(e.target.value)}
                style={{ width: '100%', boxSizing: 'border-box', padding: '0.5rem 0.75rem', borderRadius: '0.375rem', border: '1px solid #cbd5e1' }}
              />
            </div>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, marginBottom: '0.25rem' }}>জেলা / শহর</label>
              <input
                type="text"
                value={city}
                onChange={(e) => setCity(e.target.value)}
                style={{ width: '100%', boxSizing: 'border-box', padding: '0.5rem 0.75rem', borderRadius: '0.375rem', border: '1px solid #cbd5e1' }}
              />
            </div>
          </div>

          <div style={{ display: 'flex', gap: '1rem', justifyContent: 'space-between', alignItems: 'center' }}>
            <button
              type="button"
              onClick={handleApplyRmgTemplate}
              disabled={loading}
              style={{ padding: '0.625rem 1.25rem', borderRadius: '0.375rem', border: '1px solid #10b981', backgroundColor: '#ecfdf5', color: '#047857', fontWeight: 600, cursor: 'pointer' }}
            >
              ⚡ ১-ক্লিকে RMG রেডি টেমপ্লেট ব্যবহার করুন
            </button>
            <button
              type="button"
              onClick={() => setStep(2)}
              style={{ padding: '0.625rem 1.25rem', borderRadius: '0.375rem', border: 'none', backgroundColor: '#10b981', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
            >
              পরবর্তী ধাপ →
            </button>
          </div>
        </div>
      )}

      {step === 2 && (
        <form onSubmit={handleSaveProfile} style={{ backgroundColor: '#fff', padding: '2rem', borderRadius: '0.75rem', border: '1px solid #e2e8f0', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.05)' }}>
          <h2 style={{ fontSize: '1.25rem', fontWeight: 600, marginBottom: '1rem' }}>এনার্জি সরঞ্জাম ও সুবিধার বিবরণ</h2>
          
          <div style={{ display: 'grid', gap: '1.25rem', marginBottom: '1.5rem' }}>
            <div style={{ display: 'flex', gap: '2rem' }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer', fontWeight: 500 }}>
                <input
                  type="checkbox"
                  checked={hasGenset}
                  onChange={(e) => setHasGenset(e.target.checked)}
                />
                ডিজেল জেনারেটর রয়েছে (Diesel Genset)
              </label>

              <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer', fontWeight: 500 }}>
                <input
                  type="checkbox"
                  checked={hasBoiler}
                  onChange={(e) => setHasBoiler(e.target.checked)}
                />
                গ্যাস / স্টিম বয়লার রয়েছে (Steam Boiler)
              </label>
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, marginBottom: '0.25rem' }}>
                কারখানার ছাদের আয়তন (Rooftop Solar Area in Sq Ft)
              </label>
              <input
                type="number"
                value={roofAreaSqFt}
                onChange={(e) => setRoofAreaSqFt(Number(e.target.value))}
                style={{ width: '100%', boxSizing: 'border-box', padding: '0.5rem 0.75rem', borderRadius: '0.375rem', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, marginBottom: '0.25rem' }}>
                বিল্ডিং মালিকানা (Building Ownership)
              </label>
              <select
                value={buildingOwnership}
                onChange={(e) => setBuildingOwnership(e.target.value)}
                style={{ width: '100%', boxSizing: 'border-box', padding: '0.5rem 0.75rem', borderRadius: '0.375rem', border: '1px solid #cbd5e1' }}
              >
                <option value="Owned">নিজস্ব (Owned)</option>
                <option value="Leased">ভাড়া (Leased)</option>
              </select>
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, marginBottom: '0.25rem' }}>
                বার্ষিক পোশাক উৎপাদন ক্ষমতা (Annual Garment Production Pieces)
              </label>
              <input
                type="number"
                value={annualPieces}
                onChange={(e) => setAnnualPieces(Number(e.target.value))}
                style={{ width: '100%', boxSizing: 'border-box', padding: '0.5rem 0.75rem', borderRadius: '0.375rem', border: '1px solid #cbd5e1' }}
              />
            </div>
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between' }}>
            <button
              type="button"
              onClick={() => setStep(1)}
              style={{ padding: '0.625rem 1.25rem', borderRadius: '0.375rem', border: '1px solid #cbd5e1', backgroundColor: '#fff', color: '#475569', fontWeight: 600, cursor: 'pointer' }}
            >
              ← পূর্ববর্তী ধাপ
            </button>
            <button
              type="submit"
              disabled={loading}
              style={{ padding: '0.625rem 1.25rem', borderRadius: '0.375rem', border: 'none', backgroundColor: '#10b981', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
            >
              {loading ? 'সংরক্ষণ হচ্ছে...' : 'সংরক্ষণ ও শিডিউল দেখুন →'}
            </button>
          </div>
        </form>
      )}

      {step === 3 && (
        <div style={{ backgroundColor: '#fff', padding: '2rem', borderRadius: '0.75rem', border: '1px solid #e2e8f0', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.05)' }}>
          <div style={{ textAlign: 'center', marginBottom: '1.5rem' }}>
            <div style={{ fontSize: '3rem', marginBottom: '0.5rem' }}>🎉</div>
            <h2 style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#0f172a' }}>অনবোর্ডিং সফলভাবে সম্পন্ন হয়েছে!</h2>
            <p style={{ color: '#64748b' }}>আপনার কারখানার মাসিক বিল ট্র্যাকিং এবং এনার্জি অডিট সিস্টেম প্রস্তুত।</p>
          </div>

          <div style={{ backgroundColor: '#f8fafc', padding: '1rem', borderRadius: '0.5rem', border: '1px solid #e2e8f0', marginBottom: '1.5rem' }}>
            <h3 style={{ fontSize: '1rem', fontWeight: 600, marginBottom: '0.5rem' }}>সক্রিয় ট্র্যাকিং ক্যালেন্ডার:</h3>
            <ul style={{ paddingLeft: '1.25rem', margin: 0, color: '#475569', fontSize: '0.875rem', display: 'grid', gap: '0.5rem' }}>
              <li><strong>DESCO বিদ্যুৎ বিল:</strong> প্রতি মাসের ১০ তারিখের মধ্যে</li>
              <li><strong>ডিজেল জ্বালানি স্লিপ:</strong> প্রতি মাসের ৫ তারিখের মধ্যে</li>
              <li><strong>তিতাস গ্যাস বিল:</strong> প্রতি মাসের ১৫ তারিখের মধ্যে</li>
            </ul>
          </div>

          <div style={{ display: 'flex', justifyContent: 'center', gap: '1rem' }}>
            <a
              href="/capture"
              style={{ display: 'inline-block', padding: '0.75rem 1.5rem', borderRadius: '0.5rem', backgroundColor: '#10b981', color: '#fff', fontWeight: 600, textDecoration: 'none' }}
            >
              📷 বিল ছবি তোলা শুরু করুন
            </a>
            <a
              href="/dashboard"
              style={{ display: 'inline-block', padding: '0.75rem 1.5rem', borderRadius: '0.5rem', backgroundColor: '#334155', color: '#fff', fontWeight: 600, textDecoration: 'none' }}
            >
              📊 ড্যাশবোর্ডে যান
            </a>
          </div>
        </div>
      )}
    </div>
  );
};
