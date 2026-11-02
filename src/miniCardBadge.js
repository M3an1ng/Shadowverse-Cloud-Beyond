import "./styles/mini-card-badge.css";
let badgeSequence = 0;
export function miniCardBadge(kind, value) {
  const regions = {
    cost: { x: 15, y: 58, width: 167, height: 167 },
    attack: { x: 27, y: 771, width: 157, height: 182 },
    health: { x: 582, y: 777, width: 142, height: 175 },
  };
  const r = regions[kind];
  const clip = "mini-badge-" + ++badgeSequence;
  const ns = "http://www.w3.org/2000/svg";
  const root = document.createElement("span");
  root.className = `card-badge card-badge-${kind} catalog-badge ${kind}`;
  root.dataset.digits = String(value).length;
  root.setAttribute(
    "aria-label",
    `${{ cost: "费用", attack: "攻击", health: "生命" }[kind]} ${value}`,
  );
  const svg = document.createElementNS(ns, "svg");
  svg.setAttribute("viewBox", `0 0 ${r.width} ${r.height}`);
  svg.setAttribute(
    "preserveAspectRatio",
    kind === "attack"
      ? "xMinYMid meet"
      : kind === "health"
        ? "xMaxYMid meet"
        : "xMidYMid meet",
  );
  svg.setAttribute("aria-hidden", "true");
  const defs = document.createElementNS(ns, "defs"),
    clipPath = document.createElementNS(ns, "clipPath");
  clipPath.id = clip;
  const shape = document.createElementNS(
    ns,
    kind === "cost" ? "circle" : "polygon",
  );
  if (kind === "cost") {
    shape.setAttribute("cx", "83.5");
    shape.setAttribute("cy", "83.5");
    shape.setAttribute("r", "82");
  } else
    shape.setAttribute(
      "points",
      kind === "attack"
        ? "78,3 142,80 119,120 148,120 136,155 108,155 103,177 51,177 46,155 16,155 4,122 33,122 13,80"
        : "69,3 129,26 137,96 130,117 113,139 69,171 27,139 10,117 3,96 10,26",
    );
  clipPath.append(shape);
  defs.append(clipPath);
  const img = document.createElementNS(ns, "image");
  img.setAttribute("clip-path", `url(#${clip})`);
  img.setAttribute("href", "/assets/card-ui/mse-beyond-badges.png");
  img.setAttribute("x", -r.x);
  img.setAttribute("y", -r.y);
  img.setAttribute("width", "736");
  img.setAttribute("height", "967");
  svg.append(defs, img);
  const number = document.createElement("span");
  number.className = "card-badge-number";
  number.textContent = value;
  root.append(svg, number);
  return root;
}
