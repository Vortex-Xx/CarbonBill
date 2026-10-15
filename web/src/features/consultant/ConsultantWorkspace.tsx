import React, { useState, useEffect } from 'react';
import { apiClient, getStoredUser, setSession, AuthUser } from '../../lib/apiClient';

interface FactorItem {
  id: string;
  activityType: string;
  factorValue: number;
  unit: string;
  scope: number;
  source: string;
  isOverridden: boolean;
  justification?: string;
}

export const ConsultantWorkspace: React.FC = () => {
  const user = getStoredUser();
  const [factors, setFactors] = useState<FactorItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [switching, setSwitching] = useState(false);

  // Override Form Modal State
  const [selectedFactor, setSelectedFactor] = useState<FactorItem | null>(null);
  const [overrideValue, setOverrideValue] = useState<number>(0);
  const [justification, setJustification] = useState('');
  const [msg, setMsg] = useState<string | null>(null);

  useEffect(() => {
    loadFactors();
  }, [user?.activeOrgId]);

  const loadFactors = async () => {
    try {
      setLoading(true);
      const data = await apiClient.get<FactorItem[]>('/api/v1/factors');
      setFactors(data);
    } catch {
      // Handle when not authorized or demo
    } finally {
      setLoading(false);
    }
  };

  const handleSwitchOrg = async (targetOrgId: string) => {
    setSwitching(true);
    try {
      const res = await apiClient.post<{
        accessToken: string;
        activeOrgId: string;
        activeOrgName: string;
        memberships: Array<{ orgId: string; orgName: string }>;
      }>('/api/v1/auth/switch-org', { targetOrgId });

      if (user) {
        const updatedUser: AuthUser = {
          ...user,
          activeOrgId: res.activeOrgId,
          activeOrgName: res.activeOrgName,
          memberships: res.memberships,
        };
        setSession(res.accessToken, updatedUser);
        window.location.reload();
      }
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'ফ্যাক্টরি পরিবর্তন ব্যর্থ হয়েছে।');
    } finally {
      setSwitching(false);
    }
  };

  const handleSaveOverride = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedFactor) return;

    try {
      await apiClient.post('/api/v1/factor-overrides', {
        factorId: selectedFactor.id,
        overrideValue,
        justification,
      });

      setMsg('ফ্যাক্টর ওভাররাইড সফলভাবে অনুমোদন এবং প্রয়োগ করা হয়েছে!');
      setSelectedFactor(null);
      await loadFactors();
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'ওভাররাইড ব্যর্থ হয়েছে।');
    }
  };

  return (
    <div style={{ maxWidth: '1000px', margin: '2rem auto', padding: '1.5rem', fontFamily: 'system-ui, sans-serif', color: '#1e293b' }}>
      <div style={{ marginBottom: '2rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <div>
          <h1 style={{ fontSize: '1.75rem', fontWeight: 'bold', margin: '0 0 0.25rem 0', color: '#0f172a' }}>
            কনসালট্যান্ট ওয়ার্কস্পেস (Consultant Workspace)
          </h1>
          <p style={{ color: '#64748b', margin: 0, fontSize: '0.9rem' }}>
            মাল্টি-ফ্যাক্টরি পোর্টফোলিও পর্যবেক্ষণ, এমিশন ফ্যাক্টর কাস্টমাইজেশন ও অডিট সাপোর্ট
          </p>
        </div>

        {/* Org Switcher */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <span style={{ fontSize: '0.875rem', color: '#64748b', fontWeight: 500 }}>বর্তমান ক্লায়েন্ট:</span>
          <select
            value={user?.activeOrgId}
            disabled={switching}
            onChange={(e) => handleSwitchOrg(e.target.value)}
            style={{ padding: '0.5rem 0.75rem', borderRadius: '0.375rem', border: '1px solid #cbd5e1', fontWeight: 600, backgroundColor: '#fff', color: '#0f172a' }}
          >
            {user?.memberships?.map(m => (
              <option key={m.orgId} value={m.orgId}>
                {m.orgName}
              </option>
            ))}
          </select>
        </div>
      </div>

      {msg && (
        <div style={{ padding: '0.75rem 1rem', borderRadius: '0.5rem', backgroundColor: '#ecfdf5', border: '1px solid #10b981', color: '#047857', marginBottom: '1.5rem', fontSize: '0.875rem' }}>
          {msg}
        </div>
      )}

      {/* Factor Registry & Override Section */}
      <div style={{ backgroundColor: '#fff', padding: '1.5rem', borderRadius: '0.75rem', border: '1px solid #e2e8f0', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
          <h2 style={{ fontSize: '1.2rem', fontWeight: 600, margin: 0, color: '#0f172a' }}>
            সক্রিয় এমিশন ফ্যাক্টর ও কাস্টম ওভাররাইড তালিকা
          </h2>
          <span style={{ fontSize: '0.8rem', color: '#64748b' }}>GWP Basis: IPCC AR6</span>
        </div>

        {loading ? (
          <div style={{ padding: '2rem', textAlign: 'center', color: '#64748b' }}>ফ্যাক্টর তালিকা লোড হচ্ছে...</div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem', textAlign: 'left' }}>
              <thead>
                <tr style={{ borderBottom: '2px solid #e2e8f0', color: '#64748b' }}>
                  <th style={{ padding: '0.75rem' }}>অ্যাক্টিভিটি ধরণ</th>
                  <th style={{ padding: '0.75rem' }}>স্কোপ</th>
                  <th style={{ padding: '0.75rem' }}>প্রযোজ্য ফ্যাক্টর (kg CO2e)</th>
                  <th style={{ padding: '0.75rem' }}>একক</th>
                  <th style={{ padding: '0.75rem' }}>সোর্স সাইটেশন</th>
                  <th style={{ padding: '0.75rem' }}>অ্যাকশন</th>
                </tr>
              </thead>
              <tbody>
                {factors.map(f => (
                  <tr key={f.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                    <td style={{ padding: '0.75rem', fontWeight: 600, color: '#0f172a' }}>
                      {f.activityType}
                      {f.isOverridden && (
                        <span style={{ marginLeft: '0.5rem', padding: '0.125rem 0.375rem', borderRadius: '4px', fontSize: '0.7rem', fontWeight: 'bold', backgroundColor: '#fef3c7', color: '#92400e' }}>
                          কাস্টম ওভাররাইড
                        </span>
                      )}
                    </td>
                    <td style={{ padding: '0.75rem' }}>Scope {f.scope}</td>
                    <td style={{ padding: '0.75rem', fontWeight: 'bold', color: f.isOverridden ? '#b45309' : '#0f172a' }}>
                      {f.factorValue.toFixed(6)}
                    </td>
                    <td style={{ padding: '0.75rem' }}>/{f.unit}</td>
                    <td style={{ padding: '0.75rem', color: '#64748b', fontSize: '0.8rem' }}>{f.source}</td>
                    <td style={{ padding: '0.75rem' }}>
                      <button
                        type="button"
                        onClick={() => {
                          setSelectedFactor(f);
                          setOverrideValue(f.factorValue);
                          setJustification(f.justification || '');
                        }}
                        style={{ padding: '0.375rem 0.75rem', borderRadius: '0.375rem', border: '1px solid #cbd5e1', backgroundColor: '#f8fafc', color: '#334155', fontWeight: 500, cursor: 'pointer', fontSize: '0.8rem' }}
                      >
                        ✏️ ওভাররাইড
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Override Modal */}
      {selectedFactor && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 50, padding: '1rem' }}>
          <div style={{ backgroundColor: '#fff', borderRadius: '0.75rem', padding: '1.5rem', maxWidth: '480px', width: '100%', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.3)' }}>
            <h3 style={{ fontSize: '1.2rem', fontWeight: 600, margin: '0 0 0.5rem 0', color: '#0f172a' }}>
              এমিশন ফ্যাক্টর ওভাররাইড ফর্ম
            </h3>
            <p style={{ fontSize: '0.85rem', color: '#64748b', marginBottom: '1rem' }}>
              {selectedFactor.activityType} ({selectedFactor.unit}) এর জন্য ফ্যাক্টরি-নির্দিষ্ট পরীক্ষিত মান প্রদান করুন।
            </p>

            <form onSubmit={handleSaveOverride} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
              <div>
                <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: 500, marginBottom: '0.25rem' }}>
                  কাস্টম ফ্যাক্টর মান (kg CO2e / {selectedFactor.unit})
                </label>
                <input
                  type="number"
                  step="0.000001"
                  value={overrideValue}
                  onChange={(e) => setOverrideValue(Number(e.target.value))}
                  required
                  style={{ width: '100%', boxSizing: 'border-box', padding: '0.5rem', borderRadius: '0.375rem', border: '1px solid #cbd5e1' }}
                />
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '0.85rem', fontWeight: 500, marginBottom: '0.25rem' }}>
                  বাধ্যতামূলক যাচাইকরণ যুক্তি (Mandatory Verification Justification)
                </label>
                <textarea
                  rows={3}
                  value={justification}
                  onChange={(e) => setJustification(e.target.value)}
                  required
                  placeholder="যেমন: অন-সাইট সোলার জেনারেশন অডিট রিপোর্ট অনুযায়ী পরীক্ষিত গ্রিড মিশ্রণ।"
                  style={{ width: '100%', boxSizing: 'border-box', padding: '0.5rem', borderRadius: '0.375rem', border: '1px solid #cbd5e1', fontSize: '0.85rem' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem', marginTop: '0.5rem' }}>
                <button
                  type="button"
                  onClick={() => setSelectedFactor(null)}
                  style={{ padding: '0.5rem 1rem', borderRadius: '0.375rem', border: '1px solid #cbd5e1', backgroundColor: '#fff', cursor: 'pointer' }}
                >
                  বাতিল
                </button>
                <button
                  type="submit"
                  style={{ padding: '0.5rem 1rem', borderRadius: '0.375rem', border: 'none', backgroundColor: '#10b981', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  সংরক্ষণ ও প্রয়োগ
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};
