import React, { useState, useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Leaf, Lock, Mail, Building, User, Phone, CheckCircle, ArrowRight } from 'lucide-react';
import { setSession } from '../../lib/apiClient';

interface LoginViewProps {
  onLoginSuccess: () => void;
  onGoToFloor: () => void;
}

export default function LoginView({ onLoginSuccess, onGoToFloor }: LoginViewProps) {
  const { t, i18n } = useTranslation();
  const [tab, setTab] = useState<'login' | 'register' | 'invite'>('login');

  // Login form state
  const [loginIdentifier, setLoginIdentifier] = useState('owner@apex.local');
  const [password, setPassword] = useState('Pass1234!');

  // Register form state
  const [regOrgName, setRegOrgName] = useState('');
  const [regFullName, setRegFullName] = useState('');
  const [regEmail, setRegEmail] = useState('');
  const [regPassword, setRegPassword] = useState('');
  const [regFacilityType, setRegFacilityType] = useState('RMG_Woven');

  // Invite acceptance state
  const [inviteToken, setInviteToken] = useState('');
  const [inviteFullName, setInviteFullName] = useState('');
  const [invitePassword, setInvitePassword] = useState('');

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  useEffect(() => {
    const params = new URLSearchParams(window.location.search);
    const inv = params.get('invite');
    if (inv) {
      setInviteToken(inv);
      setTab('invite');
    }
  }, []);

  async function handleLogin(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const isPhone = /^[0-9+]+$/.test(loginIdentifier.replace(/[\s-]/g, ''));
      const payload: Record<string, string> = { password };
      if (isPhone) {
        payload.phoneNumber = loginIdentifier;
      } else {
        payload.email = loginIdentifier;
      }

      const res = await fetch('/api/v1/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error(data.detail || t('login.loginFailed') || 'লগইন ব্যর্থ হয়েছে। ইমেইল ও পাসওয়ার্ড যাচাই করুন।');
      }

      const data = await res.json();
      setSession(data.accessToken, {
        userId: data.userId,
        email: data.email,
        fullName: data.fullName,
        activeOrgId: data.activeOrgId,
        activeOrgName: data.activeOrgName,
        memberships: data.memberships
      });

      onLoginSuccess();
    } catch (err: any) {
      setError(err.message || 'Login failed');
    } finally {
      setLoading(false);
    }
  }

  async function handleRegister(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const res = await fetch('/api/v1/auth/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          organizationName: regOrgName,
          fullName: regFullName,
          email: regEmail,
          password: regPassword,
          facilityType: regFacilityType
        })
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error(data.detail || 'নিবন্ধন ব্যর্থ হয়েছে। পুনরায় চেষ্টা করুন।');
      }

      const data = await res.json();
      setSession(data.accessToken, {
        userId: data.userId,
        email: data.email,
        fullName: data.fullName,
        activeOrgId: data.activeOrgId,
        activeOrgName: data.activeOrgName,
        memberships: data.memberships
      });

      onLoginSuccess();
    } catch (err: any) {
      setError(err.message || 'Registration failed');
    } finally {
      setLoading(false);
    }
  }

  async function handleAcceptInvite(e: React.FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const res = await fetch('/api/v1/auth/accept-invite', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          token: inviteToken,
          fullName: inviteFullName,
          password: invitePassword
        })
      });

      if (!res.ok) {
        const data = await res.json().catch(() => ({}));
        throw new Error(data.detail || 'আমন্ত্রণ গ্রহণ ব্যর্থ হয়েছে। লিঙ্কটি সঠিক কি না যাচাই করুন।');
      }

      const data = await res.json();
      setSession(data.accessToken, {
        userId: data.userId,
        email: data.email,
        fullName: data.fullName,
        activeOrgId: data.activeOrgId,
        activeOrgName: data.activeOrgName,
        memberships: data.memberships
      });

      onLoginSuccess();
    } catch (err: any) {
      setError(err.message || 'Invite acceptance failed');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-teal-900 via-slate-900 to-teal-950 flex flex-col justify-center items-center p-4 font-sans text-slate-100">
      <div className="max-w-md w-full bg-white rounded-3xl p-6 md:p-8 text-slate-900 shadow-2xl space-y-6">
        {/* Branding & Language */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <div className="p-2.5 bg-teal-600 rounded-2xl text-white shadow-md">
              <Leaf size={28} />
            </div>
            <div>
              <h1 className="text-2xl font-black text-teal-900">{t('appName')}</h1>
              <p className="text-xs text-slate-500">{t('tagline')}</p>
            </div>
          </div>

          <button
            onClick={() => i18n.changeLanguage(i18n.language === 'bn' ? 'en' : 'bn')}
            className="text-xs font-bold px-2.5 py-1 bg-slate-100 hover:bg-slate-200 text-slate-700 rounded-lg transition"
          >
            {i18n.language === 'bn' ? 'English' : 'বাংলা'}
          </button>
        </div>

        {/* Tab Selector */}
        <div className="flex bg-slate-100 p-1 rounded-2xl text-xs font-bold">
          <button
            type="button"
            onClick={() => { setTab('login'); setError(null); }}
            className={`flex-1 py-2 rounded-xl transition-all ${
              tab === 'login' ? 'bg-white shadow-xs text-teal-900' : 'text-slate-500 hover:text-slate-800'
            }`}
          >
            {i18n.language === 'bn' ? 'লগইন' : 'Sign In'}
          </button>
          <button
            type="button"
            onClick={() => { setTab('register'); setError(null); }}
            className={`flex-1 py-2 rounded-xl transition-all ${
              tab === 'register' ? 'bg-white shadow-xs text-teal-900' : 'text-slate-500 hover:text-slate-800'
            }`}
          >
            {i18n.language === 'bn' ? 'নতুন প্রতিষ্ঠান' : 'Register Org'}
          </button>
          {inviteToken && (
            <button
              type="button"
              onClick={() => { setTab('invite'); setError(null); }}
              className={`flex-1 py-2 rounded-xl transition-all ${
                tab === 'invite' ? 'bg-white shadow-xs text-teal-900' : 'text-slate-500 hover:text-slate-800'
              }`}
            >
              {i18n.language === 'bn' ? 'আমন্ত্রণ গ্রহণ' : 'Join Org'}
            </button>
          )}
        </div>

        {/* Quick Demo Accounts Helper */}
        {tab === 'login' && (
          <div className="bg-slate-50 border border-slate-200/80 rounded-2xl p-3 space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-[11px] font-bold uppercase tracking-wider text-teal-900">
                ⚡ Apex Textiles Demo Team
              </span>
              <span className="text-[10px] text-slate-400">Click to fill</span>
            </div>
            <div className="grid grid-cols-2 gap-1.5 text-left">
              {[
                { name: 'Kabir Ahmed', email: 'owner@apex.local', pass: 'Pass1234!' },
                { name: 'Rahim Mia', email: 'accountant@apex.local', pass: 'Pass1234!' },
                { name: 'Nusrat Jahan', email: 'compliance@apex.local', pass: 'Pass1234!' },
                { name: 'Jahid Hasan', email: 'floor@apex.local', pass: 'Pass1234!' }
              ].map(acc => (
                <button
                  key={acc.email}
                  type="button"
                  onClick={() => {
                    setLoginIdentifier(acc.email);
                    setPassword(acc.pass);
                    setError(null);
                  }}
                  className={`p-2 rounded-xl text-left border transition-all text-xs ${
                    loginIdentifier === acc.email
                      ? 'bg-teal-50 border-teal-500 ring-1 ring-teal-500'
                      : 'bg-white border-slate-200 hover:border-teal-300'
                  }`}
                >
                  <div className="font-bold text-slate-800 text-[11px] leading-tight">{acc.name}</div>
                  <div className="text-[10px] text-slate-500 truncate">{acc.email}</div>
                </button>
              ))}
            </div>
            <div className="text-[10px] text-slate-500 italic text-center">
              All teammates share identical full access to Apex Textiles.
            </div>
          </div>
        )}

        {error && (
          <div className="p-3 bg-red-50 border border-red-200 text-red-700 text-xs rounded-xl">
            {error}
          </div>
        )}

        {successMsg && (
          <div className="p-3 bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs rounded-xl">
            {successMsg}
          </div>
        )}

        {/* 1. Login Form */}
        {tab === 'login' && (
          <form onSubmit={handleLogin} className="space-y-4">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                {i18n.language === 'bn' ? 'ইমেইল বা ফোন নম্বর' : 'Email or Phone Number'}
              </label>
              <div className="relative">
                <Mail className="absolute left-3 top-3 text-slate-400" size={18} />
                <input
                  type="text"
                  required
                  value={loginIdentifier}
                  onChange={e => setLoginIdentifier(e.target.value)}
                  placeholder="owner@apex.local or 01700000000"
                  className="w-full pl-10 pr-3 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-sm focus:outline-none focus:border-teal-600"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                {t('login.passwordPlaceholder') || 'পাসওয়ার্ড'}
              </label>
              <div className="relative">
                <Lock className="absolute left-3 top-3 text-slate-400" size={18} />
                <input
                  type="password"
                  required
                  value={password}
                  onChange={e => setPassword(e.target.value)}
                  placeholder="••••••••"
                  className="w-full pl-10 pr-3 py-2.5 bg-slate-50 border border-slate-200 rounded-xl text-sm focus:outline-none focus:border-teal-600"
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full py-3 bg-teal-700 hover:bg-teal-800 text-white font-bold rounded-xl shadow-lg transition-all active:scale-98"
            >
              {loading ? (i18n.language === 'bn' ? 'প্রবেশ করা হচ্ছে...' : 'Signing In...') : (i18n.language === 'bn' ? 'প্রবেশ করুন' : 'Sign In')}
            </button>
          </form>
        )}

        {/* 2. Register Organization Form */}
        {tab === 'register' && (
          <form onSubmit={handleRegister} className="space-y-3">
            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                {i18n.language === 'bn' ? 'প্রতিষ্ঠানের নাম' : 'Organization Name'}
              </label>
              <div className="relative">
                <Building className="absolute left-3 top-3 text-slate-400" size={18} />
                <input
                  type="text"
                  required
                  value={regOrgName}
                  onChange={e => setRegOrgName(e.target.value)}
                  placeholder={i18n.language === 'bn' ? 'যেমন: বেঙ্গল ফ্যাশন লিমিটেড' : 'e.g. Bengal Fashion Ltd'}
                  className="w-full pl-10 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:outline-none focus:border-teal-600"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                {i18n.language === 'bn' ? 'আপনার পূর্ণ নাম' : 'Your Full Name'}
              </label>
              <div className="relative">
                <User className="absolute left-3 top-3 text-slate-400" size={18} />
                <input
                  type="text"
                  required
                  value={regFullName}
                  onChange={e => setRegFullName(e.target.value)}
                  placeholder={i18n.language === 'bn' ? 'যেমন: মোহাম্মদ রফিক' : 'e.g. Rafiqul Islam'}
                  className="w-full pl-10 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:outline-none focus:border-teal-600"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                {i18n.language === 'bn' ? 'ইমেইল ঠিকানা' : 'Email Address'}
              </label>
              <div className="relative">
                <Mail className="absolute left-3 top-3 text-slate-400" size={18} />
                <input
                  type="email"
                  required
                  value={regEmail}
                  onChange={e => setRegEmail(e.target.value)}
                  placeholder="rafiq@bengalfashion.com"
                  className="w-full pl-10 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:outline-none focus:border-teal-600"
                />
              </div>
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                {i18n.language === 'bn' ? 'কারখানার ধরণ' : 'Facility Type'}
              </label>
              <select
                value={regFacilityType}
                onChange={e => setRegFacilityType(e.target.value)}
                className="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-semibold focus:outline-none focus:border-teal-600"
              >
                <option value="RMG_Woven">{i18n.language === 'bn' ? 'ওভেন পোশাক (RMG Woven)' : 'RMG Woven Apparel'}</option>
                <option value="RMG_Knit">{i18n.language === 'bn' ? 'নিটওয়্যার পোশাক (RMG Knit)' : 'RMG Knitwear'}</option>
                <option value="Textile_Dyeing">{i18n.language === 'bn' ? 'ডাইং ও ওয়াশিং (Textile Dyeing)' : 'Textile Dyeing & Finishing'}</option>
                <option value="Jute_Spinning">{i18n.language === 'bn' ? 'পাট শিল্প (Jute Spinning)' : 'Jute & Spinning'}</option>
                <option value="Leather_Tannery">{i18n.language === 'bn' ? 'চামড়া শিল্প (Leather Tannery)' : 'Leather & Tannery'}</option>
              </select>
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                {i18n.language === 'bn' ? 'পাসওয়ার্ড' : 'Password'}
              </label>
              <div className="relative">
                <Lock className="absolute left-3 top-3 text-slate-400" size={18} />
                <input
                  type="password"
                  required
                  value={regPassword}
                  onChange={e => setRegPassword(e.target.value)}
                  placeholder={i18n.language === 'bn' ? 'কমপক্ষে ৮ অক্ষর (যেমন: Pass1234!)' : 'Minimum 8 characters (e.g. Pass1234!)'}
                  className="w-full pl-10 pr-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs focus:outline-none focus:border-teal-600"
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full py-3 bg-teal-700 hover:bg-teal-800 text-white font-bold rounded-xl shadow-lg transition-all"
            >
              {loading ? (i18n.language === 'bn' ? 'নিবন্ধন করা হচ্ছে...' : 'Creating Organization...') : (i18n.language === 'bn' ? 'প্রতিষ্ঠান শুরু করুন' : 'Create Organization')}
            </button>
          </form>
        )}

        {/* 3. Accept Teammate Invitation Form */}
        {tab === 'invite' && (
          <form onSubmit={handleAcceptInvite} className="space-y-3">
            <div className="p-3 bg-teal-50 border border-teal-200 rounded-xl text-xs text-teal-800">
              {i18n.language === 'bn'
                ? 'আপনাকে একটি প্রতিষ্ঠানে যোগদানের আমন্ত্রণ জানানো হয়েছে। আপনার নাম ও পাসওয়ার্ড সেট করুন।'
                : 'You have been invited to join an organization with full access. Set your name and password.'}
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                {i18n.language === 'bn' ? 'আমন্ত্রণ টোকেন' : 'Invite Token'}
              </label>
              <input
                type="text"
                required
                value={inviteToken}
                onChange={e => setInviteToken(e.target.value)}
                className="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs font-mono"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                {i18n.language === 'bn' ? 'আপনার নাম' : 'Your Full Name'}
              </label>
              <input
                type="text"
                required
                value={inviteFullName}
                onChange={e => setInviteFullName(e.target.value)}
                className="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                {i18n.language === 'bn' ? 'পাসওয়ার্ড সেট করুন' : 'Set Password'}
              </label>
              <input
                type="password"
                required
                value={invitePassword}
                onChange={e => setInvitePassword(e.target.value)}
                className="w-full px-3 py-2 bg-slate-50 border border-slate-200 rounded-xl text-xs"
              />
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full py-3 bg-teal-700 hover:bg-teal-800 text-white font-bold rounded-xl shadow-lg transition-all"
            >
              {loading ? (i18n.language === 'bn' ? 'যুক্ত হওয়া হচ্ছে...' : 'Joining...') : (i18n.language === 'bn' ? 'দলে যোগ দিন' : 'Join Team')}
            </button>
          </form>
        )}

        {/* Quick link to Floor capture view */}
        <div className="pt-2 border-t text-center">
          <button
            onClick={onGoToFloor}
            className="text-xs text-teal-700 font-bold hover:underline inline-flex items-center gap-1"
          >
            📱 {i18n.language === 'bn' ? 'ফ্লোর ক্যামেরা ক্যাপচার মোড' : 'Floor Camera Capture Mode'} <ArrowRight size={12} />
          </button>
        </div>
      </div>
    </div>
  );
}
