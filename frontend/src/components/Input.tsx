import './Input.module.css';

export default function Input({
  label,
  type = 'text',
  value,
  onChange,
  placeholder,
  autoFocus = false,
}: {
  label?: string;
  type?: string;
  value: string;
  onChange: (v: string) => void;
  placeholder?: string;
  autoFocus?: boolean;
}) {
  return (
    <label className="field">
      {label && <span className="field-label">{label}</span>}
      <input
        className="input"
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        autoFocus={autoFocus}
        autoComplete="off"
      />
    </label>
  );
}
