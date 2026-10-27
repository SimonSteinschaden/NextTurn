export default function DesignerLine({
  designers,
}: {
  designers?: string[] | null;
}) {
  const names = designers?.filter((name) => name.trim().length > 0) ?? [];
  return (
    <span
      className="designer-line"
      aria-label={
        names.length
          ? "Spieldesign: " + names.join(", ")
          : "Spieldesign unbekannt"
      }
    >
      {names.length ? "von " + names.join(", ") : "Spieldesign unbekannt"}
    </span>
  );
}
