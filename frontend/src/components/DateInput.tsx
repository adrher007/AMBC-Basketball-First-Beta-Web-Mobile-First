import './DateInput.module.css';

export default function DateInput({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string; // YYYY-MM-DD
  onChange: (v: string) => void;
}) {
  return (
    <label className="date-field">
      <span className="date-label">{label}</span>
      <input
        className="date-input"
        type="date"
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
    </label>
  );
}
