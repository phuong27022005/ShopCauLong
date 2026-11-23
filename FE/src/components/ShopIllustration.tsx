export function ShopIllustration({ success = false }: { success?: boolean }) {
  return <svg aria-hidden="true" viewBox="0 0 240 140" className="mx-auto h-32 w-56">
    <ellipse cx="120" cy="123" rx="90" ry="9" fill="#e2e8f0" />
    <path d="M66 46h110l-12 54H79z" fill="#ccfbf1" stroke="#0f766e" strokeWidth="3" />
    <path d="M47 29h13l20 71h86M90 55v34m25-34v34m25-34v34m-65-17h95" fill="none" stroke="#0f766e" strokeWidth="3" strokeLinecap="round" />
    <circle cx="89" cy="114" r="7" fill="#134e4a" /><circle cx="156" cy="114" r="7" fill="#134e4a" />
    <path d="m106 14 30 5-10 29-16-3z" fill="white" stroke="#94a3b8" strokeWidth="2" /><path d="m116 16 2 30m10-28-7 27" stroke="#94a3b8" strokeWidth="2" />
    {success && <><circle cx="183" cy="42" r="25" fill="#0f766e" /><path d="m171 42 8 8 16-18" fill="none" stroke="white" strokeWidth="4" strokeLinecap="round" /></>}
  </svg>
}
