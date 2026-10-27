type Props = {
  value?: number;
  onChange: (rank: number | undefined) => void;
  disabled?: boolean;
};

export default function RankFilter({ value, onChange, disabled }: Props) {
  return (
    <label className="rank-control">
      BGG-Rang bis
      <input
        type="number"
        inputMode="numeric"
        min={1}
        max={10000}
        step={1}
        placeholder="Alle"
        value={value ?? ""}
        disabled={disabled}
        title="Rang 1 bis zu diesem Wert einschließlich. Leer lassen für alle Ränge."
        onChange={(event) => {
          if (event.target.value === "") onChange(undefined);
          else if (
            event.target.validity.valid &&
            Number.isInteger(event.target.valueAsNumber)
          )
            onChange(event.target.valueAsNumber);
        }}
      />
    </label>
  );
}
