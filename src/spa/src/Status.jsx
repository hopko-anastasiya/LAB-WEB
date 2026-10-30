export const STATUSES = [
  { value: 'Working', label: 'Працює' },
  { value: 'Limited', label: 'З обмеженнями' },
  { value: 'Closed', label: 'Не працює' }
];

export default function Status({ value }) {
  const label = STATUSES.find((s) => s.value === value)?.label ?? value;
  return <span className={`lamp lamp-${String(value).toLowerCase()}`}>{label}</span>;
}
