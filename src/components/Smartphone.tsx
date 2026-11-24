import React, { useState } from 'react';
import { SocialFeedPost, PhoneContact, WeaponId, DistrictId } from '../game/types';

interface SmartphoneProps {
  isOpen: boolean;
  timeLabel: string;
  money: number;
  wantedLevel: number;
  activeProtagonist: 'tariq' | 'maya';
  districtName: string;
  onClose: () => void;
  onCallContact: (contact: PhoneContact) => void;
  onDepositMoney: (amount: number) => void;
  onSnapPhoto?: () => void;
}

const DEFAULT_POSTS: SocialFeedPost[] = [
  {
    id: 'post_1',
    author: 'MirpurBoi99',
    handle: '@mirpur_drifter',
    avatarBg: '#0284c7',
    district: 'Kawran Bazar',
    caption: 'Kawran Bazar bus drivers disobeying the laws of physics once again 💀😭',
    captionBangla: 'কাওরান বাজারে বাসের রেষারেষি! ব্রেক ছাড়াই চলছে গাড়ি!',
    hashtags: ['#DhakaTraffic', '#BusRacing', '#SaveUs', '#GTA_Dhaka'],
    likes: 14820,
    commentsCount: 632,
    timestamp: '2m ago',
    category: 'traffic',
    verified: false,
  },
  {
    id: 'post_2',
    author: 'OldDhakaVibes',
    handle: '@chawkbazar_live',
    avatarBg: '#d97706',
    district: 'Chawkbazar',
    caption: 'Wild monkey stole a whole pack of Gold Leaf from Lalbagh tea stall 🐒🚬',
    captionBangla: 'লালবাগের চায়ের দোকান থেকে বানরে আস্ত গোল্ডলিফ প্যাকেট নিয়ে হাওয়া!',
    hashtags: ['#OldDhaka', '#Lalbagh', '#ChaiTime', '#MonkeyHeist'],
    likes: 38400,
    commentsCount: 1420,
    timestamp: '18m ago',
    category: 'chaos',
    verified: true,
  },
  {
    id: 'post_3',
    author: 'HatirjheelMidnight',
    handle: '@hatirjheel_speed',
    avatarBg: '#7c3aed',
    district: 'Hatirjheel',
    caption: 'Midnight drift crew spotted under the expressway bridge! Who is driving that green CNG?! 🏎️💨',
    captionBangla: 'হাতিরঝিলে মাঝরাতে সবুজ সিএনজি দিয়ে মারাত্মক ড্রিফট! চালক কে?!',
    hashtags: ['#HatirjheelDrift', '#OldDhakaSpeed', '#TariqOnWheels'],
    likes: 52100,
    commentsCount: 2890,
    timestamp: '1h ago',
    category: 'stunt',
    verified: true,
  },
  {
    id: 'post_4',
    author: 'BurigangaNews',
    handle: '@sadarghat_daily',
    avatarBg: '#059669',
    district: 'Sadarghat',
    caption: 'Passenger launch captain pulled off a 180 handbrake reverse at Sadarghat terminal 🚢🌊',
    captionBangla: 'সদরঘাটে দোতলা লঞ্চ দিয়ে ১৮০ ডিগ্রি রিভার্স টার্ন! যাত্রীদের মধ্যে চরম উত্তেজনা!',
    hashtags: ['#SadarghatMadness', '#LaunchDrift', '#BurigangaRacer'],
    likes: 21900,
    commentsCount: 840,
    timestamp: '3h ago',
    category: 'traffic',
    verified: false,
  },
];

const CONTACTS: PhoneContact[] = [
  {
    id: 'shofi_mechanic',
    name: 'Shofi Bhai',
    banglaName: 'শফি ভাই (ধোলাইখাল)',
    title: 'Dholaikhal Master Mechanic',
    avatarColor: '#d97706',
    phone: '+880 1711-402918',
    serviceType: 'vehicle_drop',
    cost: 1200,
    description: 'Delivers a tuned getaway sedan directly to your nearest street corner.',
    banglaDescription: 'নিকটতম রাস্তায় হাই-স্পিড টিউনড সেডান গাড়ি ডেলিভারি করবে।',
  },
  {
    id: 'master_kabir',
    name: 'Master Kabir',
    banglaName: 'ওস্তাদ কবির',
    title: 'Old Dhaka Black Market Supplier',
    avatarColor: '#b91c1c',
    phone: '+880 1822-993810',
    serviceType: 'weapons',
    cost: 2500,
    description: 'Drops tactical 12G shotgun ammo and heavy armor vest.',
    banglaDescription: 'শটগান গোলাবারুদ এবং মিলিটারী বুলেটপ্রুফ ভেস্ট পাঠাবে।',
  },
  {
    id: 'inspector_rehan',
    name: 'Inspector Rehan',
    banglaName: 'ইন্সপেক্টর রেহান',
    title: 'Corrupt Metropolitan Liaison',
    avatarColor: '#2563eb',
    phone: '+880 1914-550212',
    serviceType: 'clear_heat',
    cost: 3000,
    description: 'Wipes all active police wanted stars and dispatches false radio reports.',
    banglaDescription: 'পুলিশের ওয়ান্টেড লেভেল শূন্য করে দিবে এবং ভুয়া রেডিও মেসেজ পাঠাবে।',
  },
  {
    id: 'call_partner',
    name: 'Rally Partner',
    banglaName: 'পার্টনার ব্যাকআপ',
    title: 'Dual Protagonist Tag-Team',
    avatarColor: '#059669',
    phone: '+880 1552-330190',
    serviceType: 'companion',
    cost: 0,
    description: 'Summons Maya or Tariq to ride along in your car and provide drive-by cover fire.',
    banglaDescription: 'মায়া অথবা তারেককে আপনার গাড়িতে তুলে ব্যাকআপ ও ড্রাইভ-বাই ফায়ার দিতে ডাকুন।',
  },
];

export const Smartphone: React.FC<SmartphoneProps> = ({
  isOpen,
  timeLabel,
  money,
  wantedLevel,
  activeProtagonist,
  districtName,
  onClose,
  onCallContact,
  onDepositMoney,
  onSnapPhoto,
}) => {
  const [activeApp, setActiveApp] = useState<'home' | 'dhakalive' | 'contacts' | 'camera' | 'banking'>('home');
  const [cameraFilter, setCameraFilter] = useState<'normal' | 'monsoon' | 'neon' | 'vintage'>('normal');
  const [cameraFlash, setCameraFlash] = useState(false);
  const [posts, setPosts] = useState<SocialFeedPost[]>(DEFAULT_POSTS);
  const [likedPosts, setLikedPosts] = useState<Record<string, boolean>>({});
  const [bankBalance, setBankBalance] = useState<number>(() => {
    const saved = localStorage.getItem('dhaka_bank_vault');
    return saved ? parseInt(saved, 10) : 5000;
  });

  const handleLike = (id: string) => {
    setLikedPosts((prev) => {
      const isLiked = !prev[id];
      setPosts((current) =>
        current.map((p) =>
          p.id === id ? { ...p, likes: p.likes + (isLiked ? 1 : -1) } : p,
        ),
      );
      return { ...prev, [id]: isLiked };
    });
  };

  const handleDeposit = () => {
    if (money >= 1000) {
      const newBal = bankBalance + 1000;
      setBankBalance(newBal);
      localStorage.setItem('dhaka_bank_vault', newBal.toString());
      onDepositMoney(1000);
    }
  };

  if (!isOpen) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-end sm:items-center justify-end sm:justify-end p-2 sm:p-6 bg-black/40 backdrop-blur-[2px] pointer-events-auto select-none"
      onClick={onClose}
    >
      {/* Smartphone Chassis Frame */}
      <div
        className="relative w-full max-w-[340px] h-[580px] bg-[#0c1017] rounded-[38px] p-3 border-4 border-[#334155] shadow-[0_25px_60px_rgba(0,0,0,0.95)] flex flex-col overflow-hidden animate-in slide-in-from-bottom-8 duration-200"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Dynamic Island / Speaker Pill */}
        <div className="absolute top-4 left-1/2 -translate-x-1/2 w-24 h-4 bg-black rounded-full flex items-center justify-center gap-1.5 z-40">
          <div className="w-2.5 h-2.5 bg-[#0f172a] rounded-full border border-white/20" />
          <div className="w-1.5 h-1.5 bg-[#1e293b] rounded-full" />
        </div>

        {/* Status Bar */}
        <div className="flex items-center justify-between px-3 pt-1 text-[11px] font-mono text-white/80 z-30">
          <span>{timeLabel}</span>
          <div className="flex items-center gap-1.5 text-[10px]">
            <span className="text-[#38bdf8] font-bold">5G</span>
            <span className="text-white/60">Banglalink</span>
            <span className="text-[#4ade80]">94%</span>
          </div>
        </div>

        {/* Screen Container */}
        <div className="relative flex-1 mt-2 rounded-[28px] overflow-hidden bg-gradient-to-b from-[#111827] to-[#0a0e14] flex flex-col text-white">
          {/* APP 1: HOME SCREEN */}
          {activeApp === 'home' && (
            <div className="flex-1 p-4 flex flex-col justify-between">
              {/* Top Greeting & District Banner */}
              <div className="mt-4 text-center">
                <div className="text-[10px] uppercase font-bold tracking-widest text-[#f5d07a] font-display">
                  DhakaOS 6.0 Pro
                </div>
                <div className="text-xl font-bold font-display mt-0.5">
                  {activeProtagonist === 'tariq' ? 'Tariq · তারেক' : 'Maya · মায়া'}
                </div>
                <div className="text-xs text-white/60 font-bangla mt-0.5">{districtName}</div>

                {wantedLevel > 0 && (
                  <div className="mt-2 inline-flex items-center gap-1 px-2.5 py-0.5 bg-red-600/90 text-white rounded-full text-[10px] font-bold uppercase tracking-wider animate-pulse">
                    🚨 {wantedLevel} STAR HEAT DETECTED
                  </div>
                )}
              </div>

              {/* App Grid */}
              <div className="grid grid-cols-2 gap-3 my-auto">
                {/* DhakaLive (Reels/TikTok) */}
                <button
                  type="button"
                  onClick={() => setActiveApp('dhakalive')}
                  className="flex flex-col items-center justify-center p-3 rounded-2xl bg-gradient-to-br from-[#ec4899] to-[#8b5cf6] hover:scale-105 active:scale-95 transition-transform shadow-lg cursor-pointer"
                >
                  <span className="text-3xl mb-1">📱</span>
                  <span className="text-xs font-bold font-display tracking-wide">DhakaLive</span>
                  <span className="text-[9px] text-white/80 font-bangla">ভাইরাল রিলস</span>
                </button>

                {/* Contacts & Fixers */}
                <button
                  type="button"
                  onClick={() => setActiveApp('contacts')}
                  className="flex flex-col items-center justify-center p-3 rounded-2xl bg-gradient-to-br from-[#059669] to-[#0284c7] hover:scale-105 active:scale-95 transition-transform shadow-lg cursor-pointer"
                >
                  <span className="text-3xl mb-1">📞</span>
                  <span className="text-xs font-bold font-display tracking-wide">Contacts</span>
                  <span className="text-[9px] text-white/80 font-bangla">সিন্ডিকেট হেল্প</span>
                </button>

                {/* Camera */}
                <button
                  type="button"
                  onClick={() => setActiveApp('camera')}
                  className="flex flex-col items-center justify-center p-3 rounded-2xl bg-gradient-to-br from-[#f59e0b] to-[#ea580c] hover:scale-105 active:scale-95 transition-transform shadow-lg cursor-pointer"
                >
                  <span className="text-3xl mb-1">📸</span>
                  <span className="text-xs font-bold font-display tracking-wide">DhakaCam</span>
                  <span className="text-[9px] text-white/80 font-bangla">ক্যামেরা মোড</span>
                </button>

                {/* bKash Banking */}
                <button
                  type="button"
                  onClick={() => setActiveApp('banking')}
                  className="flex flex-col items-center justify-center p-3 rounded-2xl bg-gradient-to-br from-[#e11d48] to-[#9333ea] hover:scale-105 active:scale-95 transition-transform shadow-lg cursor-pointer"
                >
                  <span className="text-3xl mb-1">💳</span>
                  <span className="text-xs font-bold font-display tracking-wide">bKash Vault</span>
                  <span className="text-[9px] text-white/80 font-bangla">ব্যাংক ব্যালেন্স</span>
                </button>
              </div>

              {/* Bottom Quick Info */}
              <div className="bg-black/40 border border-white/10 rounded-xl p-2.5 flex items-center justify-between text-xs font-mono">
                <span className="text-white/60">Cash in Hand:</span>
                <span className="font-bold text-[#4ade80]">৳{money.toLocaleString()}</span>
              </div>
            </div>
          )}

          {/* APP 2: DHAKALIVE (VIRAL VIDEO / REELS FEED) */}
          {activeApp === 'dhakalive' && (
            <div className="flex-1 flex flex-col overflow-hidden bg-black">
              {/* Header */}
              <div className="p-2.5 border-b border-white/10 flex items-center justify-between bg-[#111827]">
                <button
                  type="button"
                  onClick={() => setActiveApp('home')}
                  className="text-xs text-[#38bdf8] font-bold cursor-pointer"
                >
                  ← Home
                </button>
                <span className="font-display text-xs font-bold uppercase tracking-wider text-[#ec4899]">
                  🔥 DhakaLive Viral
                </span>
                <span className="text-[10px] text-white/50">LIVE</span>
              </div>

              {/* Feed Stream */}
              <div className="flex-1 overflow-y-auto p-3 flex flex-col gap-3">
                {posts.map((post) => (
                  <div
                    key={post.id}
                    className="bg-[#18202f] border border-white/10 rounded-2xl p-3 shadow-md flex flex-col gap-2"
                  >
                    {/* Author Bar */}
                    <div className="flex items-center gap-2">
                      <div
                        className="w-7 h-7 rounded-full flex items-center justify-center font-bold text-xs"
                        style={{ backgroundColor: post.avatarBg }}
                      >
                        {post.author.charAt(0)}
                      </div>
                      <div className="flex flex-col leading-tight">
                        <div className="flex items-center gap-1">
                          <span className="text-xs font-bold text-white">{post.author}</span>
                          {post.verified && <span className="text-[10px] text-[#38bdf8]">✓</span>}
                        </div>
                        <span className="text-[10px] text-white/50">{post.handle} · {post.timestamp}</span>
                      </div>
                    </div>

                    {/* Simulated Video Preview Frame */}
                    <div className="w-full h-32 rounded-xl bg-gradient-to-br from-[#1e293b] to-[#0f172a] border border-white/5 flex flex-col items-center justify-center text-center p-2 relative overflow-hidden group">
                      <div className="text-2xl mb-1 animate-bounce">
                        {post.category === 'traffic' ? '🚌' : post.category === 'stunt' ? '🏎️' : '🐒'}
                      </div>
                      <div className="text-[11px] font-bold text-white leading-tight px-1 drop-shadow">
                        {post.caption}
                      </div>
                      <div className="text-[10px] text-[#f5d07a] font-bangla mt-0.5 drop-shadow">
                        {post.captionBangla}
                      </div>
                      {/* Live Badge */}
                      <div className="absolute top-2 right-2 bg-red-600/90 text-[8px] font-bold uppercase tracking-widest px-1.5 py-0.5 rounded text-white">
                        LIVE CLIP
                      </div>
                    </div>

                    {/* Hashtags */}
                    <div className="flex flex-wrap gap-1 text-[10px] font-mono text-[#38bdf8]">
                      {post.hashtags.map((tag) => (
                        <span key={tag}>{tag}</span>
                      ))}
                    </div>

                    {/* Actions Bar */}
                    <div className="flex items-center justify-between border-t border-white/10 pt-2 text-xs font-mono text-white/70">
                      <button
                        type="button"
                        onClick={() => handleLike(post.id)}
                        className={`flex items-center gap-1 cursor-pointer transition-colors ${
                          likedPosts[post.id] ? 'text-red-500 font-bold' : 'hover:text-red-400'
                        }`}
                      >
                        <span>{likedPosts[post.id] ? '❤️' : '🤍'}</span>
                        <span>{post.likes.toLocaleString()}</span>
                      </button>
                      <div className="flex items-center gap-1 text-white/60">
                        <span>💬</span>
                        <span>{post.commentsCount}</span>
                      </div>
                      <div className="text-[10px] text-white/40 uppercase">{post.district}</div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* APP 3: CONTACTS & FIXERS */}
          {activeApp === 'contacts' && (
            <div className="flex-1 flex flex-col overflow-hidden bg-[#0c1017]">
              {/* Header */}
              <div className="p-2.5 border-b border-white/10 flex items-center justify-between bg-[#111827]">
                <button
                  type="button"
                  onClick={() => setActiveApp('home')}
                  className="text-xs text-[#38bdf8] font-bold cursor-pointer"
                >
                  ← Home
                </button>
                <span className="font-display text-xs font-bold uppercase tracking-wider text-[#059669]">
                  Contacts & Fixers
                </span>
                <span className="text-[10px] text-white/50">4 Active</span>
              </div>

              {/* Contacts List */}
              <div className="flex-1 overflow-y-auto p-3 flex flex-col gap-2.5">
                {CONTACTS.map((c) => (
                  <div
                    key={c.id}
                    className="bg-[#18202f] border border-white/10 rounded-xl p-3 flex flex-col gap-1.5 shadow"
                  >
                    <div className="flex items-center justify-between">
                      <div className="flex items-center gap-2">
                        <div
                          className="w-8 h-8 rounded-full flex items-center justify-center font-bold text-xs"
                          style={{ backgroundColor: c.avatarColor }}
                        >
                          {c.name.charAt(0)}
                        </div>
                        <div>
                          <div className="text-xs font-bold text-white">{c.name}</div>
                          <div className="text-[10px] text-[#f5d07a] font-bangla">{c.banglaName}</div>
                        </div>
                      </div>
                      <button
                        type="button"
                        onClick={() => onCallContact(c)}
                        disabled={c.cost > money}
                        className={`px-3 py-1.5 rounded-lg text-xs font-bold uppercase tracking-wider font-display transition-all cursor-pointer ${
                          money >= c.cost
                            ? 'bg-[#059669] hover:bg-[#10b981] text-white shadow-md'
                            : 'bg-white/10 text-white/40 cursor-not-allowed'
                        }`}
                      >
                        {c.cost > 0 ? `Call ৳${c.cost}` : 'Rally Free'}
                      </button>
                    </div>
                    <div className="text-[11px] text-white/70 leading-snug">{c.description}</div>
                    <div className="text-[10px] text-white/40 font-mono">{c.phone}</div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* APP 4: CAMERA MODE */}
          {activeApp === 'camera' && (
            <div className="flex-1 flex flex-col justify-between bg-[#05080c] p-3">
              {/* Header */}
              <div className="flex items-center justify-between">
                <button
                  type="button"
                  onClick={() => setActiveApp('home')}
                  className="text-xs text-[#38bdf8] font-bold cursor-pointer"
                >
                  ← Exit
                </button>
                <span className="text-[10px] font-mono font-bold uppercase text-[#f59e0b] tracking-widest">
                  FILTER: {cameraFilter.toUpperCase()}
                </span>
              </div>

              {/* Viewfinder Target */}
              <div className="w-full h-64 border-2 border-white/20 rounded-2xl relative flex items-center justify-center overflow-hidden">
                {cameraFlash && (
                  <div className="absolute inset-0 bg-white z-20 pointer-events-none animate-in fade-in duration-75" />
                )}
                <div className="w-12 h-12 border border-white/40 rounded-full flex items-center justify-center pointer-events-none">
                  <div className="w-2 h-2 bg-red-500 rounded-full animate-ping" />
                </div>
                <div className="absolute bottom-2 left-2 text-[9px] font-mono text-white/70">
                  {districtName} · {timeLabel}
                </div>
                <div className="absolute top-2 right-2 text-[9px] font-mono text-white/60">
                  4K 60FPS HDR
                </div>
              </div>

              {/* Shutter & Filter Bar */}
              <div className="flex flex-col gap-2">
                <div className="flex justify-around text-[10px] font-mono text-white/60">
                  <button
                    type="button"
                    onClick={() => setCameraFilter('normal')}
                    className={`cursor-pointer ${cameraFilter === 'normal' ? 'text-[#f5d07a] font-bold' : ''}`}
                  >
                    Natural
                  </button>
                  <button
                    type="button"
                    onClick={() => setCameraFilter('monsoon')}
                    className={`cursor-pointer ${cameraFilter === 'monsoon' ? 'text-[#38bdf8] font-bold' : ''}`}
                  >
                    Monsoon Teal
                  </button>
                  <button
                    type="button"
                    onClick={() => setCameraFilter('neon')}
                    className={`cursor-pointer ${cameraFilter === 'neon' ? 'text-[#ec4899] font-bold' : ''}`}
                  >
                    Neon Vice
                  </button>
                  <button
                    type="button"
                    onClick={() => setCameraFilter('vintage')}
                    className={`cursor-pointer ${cameraFilter === 'vintage' ? 'text-[#eab308] font-bold' : ''}`}
                  >
                    Polaroid
                  </button>
                </div>
                <button
                  type="button"
                  onClick={() => {
                    setCameraFlash(true);
                    setTimeout(() => setCameraFlash(false), 140);
                    if (onSnapPhoto) onSnapPhoto();
                  }}
                  className="w-14 h-14 rounded-full border-4 border-white bg-white/20 hover:bg-white/40 active:scale-95 transition-transform mx-auto cursor-pointer shadow-lg"
                />
              </div>
            </div>
          )}

          {/* APP 5: BKASH VAULT BANKING */}
          {activeApp === 'banking' && (
            <div className="flex-1 flex flex-col justify-between p-4 bg-[#0c1017]">
              <div>
                <div className="flex items-center justify-between border-b border-white/10 pb-2">
                  <button
                    type="button"
                    onClick={() => setActiveApp('home')}
                    className="text-xs text-[#38bdf8] font-bold cursor-pointer"
                  >
                    ← Home
                  </button>
                  <span className="font-display text-xs font-bold text-[#e11d48]">bKash Vault</span>
                </div>

                <div className="mt-6 text-center">
                  <span className="text-[10px] text-white/50 uppercase tracking-widest font-mono">
                    Secured Savings Balance
                  </span>
                  <div className="text-3xl font-extrabold text-[#4ade80] font-mono mt-1">
                    ৳{bankBalance.toLocaleString()}
                  </div>
                  <p className="text-[11px] text-white/60 mt-2 px-2 leading-relaxed">
                    Money deposited into your bKash Vault is safe from police seizure or defeat hospital fees.
                  </p>
                </div>
              </div>

              <div className="flex flex-col gap-2">
                <button
                  type="button"
                  onClick={handleDeposit}
                  disabled={money < 1000}
                  className={`w-full py-2.5 rounded-xl font-bold font-display uppercase tracking-wider text-xs transition-all cursor-pointer ${
                    money >= 1000
                      ? 'bg-[#e11d48] hover:bg-[#f43f5e] text-white shadow-lg'
                      : 'bg-white/10 text-white/30 cursor-not-allowed'
                  }`}
                >
                  Deposit ৳1,000 from Wallet
                </button>
              </div>
            </div>
          )}
        </div>

        {/* Bottom Home Indicator Bar */}
        <div
          className="w-28 h-1 bg-white/30 rounded-full mx-auto mt-2 cursor-pointer hover:bg-white/60 transition-colors"
          onClick={() => setActiveApp('home')}
        />
      </div>
    </div>
  );
};
