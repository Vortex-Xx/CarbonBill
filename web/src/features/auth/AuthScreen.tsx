import React, { useState } from 'react';
import { apiClient, setSession, AuthUser } from '../../lib/apiClient';

interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
  userId: string;
  email: string;
  fullName: string;
  activeOrgId: string;
  activeOrgName: string;
  activeRole: string;
  memberships: Array<{ orgId: string; orgName: string; role: string }>;
}

export const AuthScreen: React.FC = () => {
  const [tab, setTab] = useState<'login' | 'qr'>('login');
  
  // Standard Login State
  const [email, setEmail] = useState('accountant@apex.local');
  const [password, setPassword] = useState('Pass1234!');
  
  // Floor Staff QR + PIN State
  const [inviteToken, setInviteToken] = useState('apex-floor-demo');
  const [pin, setPin] = useState('1234');
  const [fullName, setFullName] = useState('Jahid Hasan');
  
  // Kiosk Terminal Lock State (Prompt I9 Usability / Phase 3 Follow-up)
  const [kioskPin, setKioskPin] = useState('');
  const [isKioskLockedMode, setIsKioskLockedMode] = useState(() => {
    return localStorage.getItem('carbonbill_kiosk_locked') === 'true';
  });

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lockSuccessMsg, setLockSuccessMsg] = useState<string | null>(null);

  const handleStandardLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const res = await apiClient.post<LoginResponse>('/api/v1/auth/login', {
        email,
        password,
      });

      const user: AuthUser = {
        userId: res.userId,
        email: res.email,
        fullName: res.fullName,
        activeOrgId: res.activeOrgId,
        activeOrgName: res.activeOrgName,
        activeRole: res.activeRole,
        memberships: res.memberships,
      };

      setSession(res.accessToken, user);
      window.location.href = res.activeRole === 'FloorStaff' ? '/capture' : '/dashboard';
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'লগইন ব্যর্থ হয়েছে। ইমেইল এবং পাসওয়ার্ড যাচাই করুন।');
    } finally {
      setLoading(false);
    }
  };

  const handleQrJoin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const res = await apiClient.post<{
        accessToken: string;
        refreshToken: string;
        userId: string;
        fullName: string;
        orgId: string;
        role: string;
      }>('/api/v1/auth/join', {
        token: inviteToken,
        pin,
        fullName,
      });

      const user: AuthUser = {
        userId: res.userId,
        email: `floor_${inviteToken}@carbonbill.local`,
        fullName: res.fullName,
        activeOrgId: res.orgId,
        activeOrgName: 'Apex Textile & Garments Ltd.',
        activeRole: res.role,
        memberships: [{ orgId: res.orgId, orgName: 'Apex Textile & Garments Ltd.', role: res.role }],
      };

      setSession(res.accessToken, user);
      window.location.href = '/capture';
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'কিউআর জয়েন ব্যর্থ হয়েছে। পিন যাচাই করুন।');
    } finally {
      setLoading(false);
    }
  };

  const handleUnlockKiosk = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      await apiClient.post('/api/v1/auth/pin-lock/unlock', {
        pin: kioskPin,
      });

      localStorage.removeItem('carbonbill_kiosk_locked');
      setIsKioskLockedMode(false);
      setLockSuccessMsg('কিয়স্ক আনলক হয়েছে! ফ্লোর মোডে ফিরে যাচ্ছেন...');
      setTimeout(() => {
        window.location.href = '/capture';
      }, 500);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'পিন ভুল হয়েছে। পুনরায় চেষ্টা করুন।');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', backgroundColor: '#0f172a', padding: '1rem', fontFamily: 'system-ui, sans-serif' }}>
      <div style={{ maxWidth: '440px', width: '100%', backgroundColor: '#1e293b', borderRadius: '1rem', border: '1px solid #334155', padding: '2rem', color: '#f8fafc', boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.5)' }}>
        <div style={{ textAlign: 'center', marginBottom: '1.5rem' }}>
          <div style={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'center', width: '48px', height: '48px', borderRadius: '12px', backgroundColor: '#10b981', color: '#fff', fontSize: '24px', fontWeight: 'bold', marginBottom: '0.75rem' }}>
            🌱
          </div>
          <h1 style={{ fontSize: '1.5rem', fontWeight: 'bold', margin: '0 0 0.25rem 0' }}>CarbonBill</h1>
          <p style={{ fontSize: '0.875rem', color: '#94a3b8', margin: 0 }}>বাংলাদেশি পোশাক ও উৎপাদন শিল্পের কার্বন হিসাব প্ল্যাটফর্ম</p>
        </div>

        {/* Tab Toggle */}
        <div style={{ display: 'flex', borderRadius: '0.5rem', backgroundColor: '#0f172a', padding: '4px', marginBottom: '1.5rem' }}>
          <button
            type="button"
            onClick={() => { setTab('login'); setError(null); }}
            style={{
              flex: 1,
              padding: '0.5rem',
              borderRadius: '0.375rem',
              border: 'none',
              backgroundColor: tab === 'login' ? '#334155' : 'transparent',
              color: tab === 'login' ? '#fff' : '#94a3b8',
              fontWeight: 500,
              fontSize: '0.875rem',
              cursor: 'pointer'
            }}
          >
            অফিস ও কমপ্লায়েন্স লগইন
          </button>
          <button
            type="button"
            onClick={() => { setTab('qr'); setError(null); }}
            style={{
              flex: 1,
              padding: '0.5rem',
              borderRadius: '0.375rem',
              border: 'none',
              backgroundColor: tab === 'qr' ? '#334155' : 'transparent',
              color: tab === 'qr' ? '#fff' : '#94a3b8',
              fontWeight: 500,
              fontSize: '0.875rem',
              cursor: 'pointer'
            }}
          >
            ফ্লোর স্টাফ QR + PIN
          </button>
        </div>

        {lockSuccessMsg && (
          <div style={{ padding: '0.75rem 1rem', borderRadius: '0.5rem', backgroundColor: 'rgba(16, 185, 129, 0.15)', border: '1px solid #10b981', color: '#6ee7b7', fontSize: '0.875rem', marginBottom: '1.25rem' }}>
            {lockSuccessMsg}
          </div>
        )}

        {error && (
          <div style={{ padding: '0.75rem 1rem', borderRadius: '0.5rem', backgroundColor: 'rgba(239, 68, 68, 0.15)', border: '1px solid #ef4444', color: '#fca5a5', fontSize: '0.875rem', marginBottom: '1.25rem' }}>
            {error}
          </div>
        )}

        {isKioskLockedMode ? (
          /* Kiosk Locked Screen Overlay */
          <form onSubmit={handleUnlockKiosk} style={{ display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
            <div style={{ textAlign: 'center', padding: '1rem 0' }}>
              <div style={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'center', width: '56px', height: '56px', borderRadius: '50%', backgroundColor: '#334155', color: '#fbbf24', fontSize: '28px', marginBottom: '0.75rem' }}>
                🔒
              </div>
              <h2 style={{ fontSize: '1.25rem', fontWeight: 'bold', margin: '0 0 0.25rem 0', color: '#f8fafc' }}>
                কিয়স্ক টার্মিনাল লকড
              </h2>
              <p style={{ fontSize: '0.875rem', color: '#94a3b8', margin: 0 }}>
                শিফটের কাজ চালিয়ে যেতে ৪-সংখ্যার পিন (PIN) দিন
              </p>
            </div>

            <div>
              <input
                type="password"
                maxLength={4}
                value={kioskPin}
                onChange={(e) => setKioskPin(e.target.value)}
                autoFocus
                required
                style={{ width: '100%', boxSizing: 'border-box', padding: '0.75rem', borderRadius: '0.5rem', border: '2px solid #10b981', backgroundColor: '#0f172a', color: '#fff', fontSize: '1.5rem', letterSpacing: '0.5rem', textAlign: 'center' }}
                placeholder="••••"
              />
            </div>

            <button
              type="submit"
              disabled={loading}
              style={{
                padding: '0.75rem',
                borderRadius: '0.5rem',
                border: 'none',
                backgroundColor: '#10b981',
                color: '#fff',
                fontWeight: 'bold',
                fontSize: '0.9rem',
                cursor: loading ? 'not-allowed' : 'pointer',
                opacity: loading ? 0.7 : 1
              }}
            >
              {loading ? 'আনলক হচ্ছে...' : '🔓 আনলক করুন (Unlock Terminal)'}
            </button>

            <button
              type="button"
              onClick={() => {
                localStorage.removeItem('carbonbill_kiosk_locked');
                setIsKioskLockedMode(false);
              }}
              style={{
                background: 'none',
                border: 'none',
                color: '#94a3b8',
                fontSize: '0.8rem',
                cursor: 'pointer',
                textAlign: 'center',
                textDecoration: 'underline'
              }}
            >
              অন্য অ্যাকাউন্ট দিয়ে লগইন করুন
            </button>
          </form>
        ) : tab === 'login' ? (
          <form onSubmit={handleStandardLogin} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, marginBottom: '0.375rem', color: '#cbd5e1' }}>
                ইমেইল ঠিকানা (Email)
              </label>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                style={{ width: '100%', boxSizing: 'border-box', padding: '0.625rem 0.875rem', borderRadius: '0.5rem', border: '1px solid #475569', backgroundColor: '#0f172a', color: '#fff', fontSize: '0.875rem' }}
                placeholder="accountant@factory.com"
              />
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, marginBottom: '0.375rem', color: '#cbd5e1' }}>
                পাসওয়ার্ড (Password)
              </label>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                style={{ width: '100%', boxSizing: 'border-box', padding: '0.625rem 0.875rem', borderRadius: '0.5rem', border: '1px solid #475569', backgroundColor: '#0f172a', color: '#fff', fontSize: '0.875rem' }}
                placeholder="••••••••"
              />
            </div>

            <button
              type="submit"
              disabled={loading}
              style={{
                marginTop: '0.5rem',
                padding: '0.75rem',
                borderRadius: '0.5rem',
                border: 'none',
                backgroundColor: '#10b981',
                color: '#fff',
                fontWeight: 'bold',
                fontSize: '0.875rem',
                cursor: loading ? 'not-allowed' : 'pointer',
                opacity: loading ? 0.7 : 1
              }}
            >
              {loading ? 'লগইন হচ্ছে...' : 'প্রবেশ করুন (Sign In)'}
            </button>
          </form>
        ) : (
          <form onSubmit={handleQrJoin} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, marginBottom: '0.375rem', color: '#cbd5e1' }}>
                আপনার নাম (Full Name)
              </label>
              <input
                type="text"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                required
                style={{ width: '100%', boxSizing: 'border-box', padding: '0.625rem 0.875rem', borderRadius: '0.5rem', border: '1px solid #475569', backgroundColor: '#0f172a', color: '#fff', fontSize: '0.875rem' }}
                placeholder="জাহিদ হাসান"
              />
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, marginBottom: '0.375rem', color: '#cbd5e1' }}>
                ইনভাইট কোড / টোকেন
              </label>
              <input
                type="text"
                value={inviteToken}
                onChange={(e) => setInviteToken(e.target.value)}
                required
                style={{ width: '100%', boxSizing: 'border-box', padding: '0.625rem 0.875rem', borderRadius: '0.5rem', border: '1px solid #475569', backgroundColor: '#0f172a', color: '#fff', fontSize: '0.875rem' }}
                placeholder="apex-floor-demo"
              />
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, marginBottom: '0.375rem', color: '#cbd5e1' }}>
                ৪-সংখ্যার গোপন পিন (4-digit PIN)
              </label>
              <input
                type="password"
                maxLength={6}
                value={pin}
                onChange={(e) => setPin(e.target.value)}
                required
                style={{ width: '100%', boxSizing: 'border-box', padding: '0.625rem 0.875rem', borderRadius: '0.5rem', border: '1px solid #475569', backgroundColor: '#0f172a', color: '#fff', fontSize: '1.25rem', letterSpacing: '0.25rem', textAlign: 'center' }}
                placeholder="1234"
              />
            </div>

            <button
              type="submit"
              disabled={loading}
              style={{
                marginTop: '0.5rem',
                padding: '0.75rem',
                borderRadius: '0.5rem',
                border: 'none',
                backgroundColor: '#10b981',
                color: '#fff',
                fontWeight: 'bold',
                fontSize: '0.875rem',
                cursor: loading ? 'not-allowed' : 'pointer',
                opacity: loading ? 0.7 : 1
              }}
            >
              {loading ? 'যাচাই হচ্ছে...' : 'ক্যামেরা চালু করুন (Start Capture)'}
            </button>
          </form>
        )}
      </div>
    </div>
  );
};
