import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  BarElement,
  PointElement,
  LineElement,
  ArcElement,
  Title,
  Tooltip,
  Legend,
  Filler,
} from 'chart.js';

let isRegistered = false;

export function ensureChartRegistered() {
  if (isRegistered) return;
  ChartJS.register(
    CategoryScale,
    LinearScale,
    BarElement,
    PointElement,
    LineElement,
    ArcElement,
    Title,
    Tooltip,
    Legend,
    Filler
  );
  isRegistered = true;
}

/**
 * Creates a diagonal hatch pattern for Canvas to visually depict estimated emissions.
 * Adheres to Design Principle: "Honest numbers: estimates are explicitly tagged and visually hatched in charts"
 */
export function createHatchPattern(stripeColor: string = '#f59e0b', bgColor: string = '#fef3c7'): CanvasPattern | string {
  if (typeof document === 'undefined') return stripeColor;
  try {
    const canvas = document.createElement('canvas');
    canvas.width = 12;
    canvas.height = 12;
    const ctx = canvas.getContext('2d');
    if (!ctx) return stripeColor;

    // Fill background
    ctx.fillStyle = bgColor;
    ctx.fillRect(0, 0, 12, 12);

    // Diagonal stripes
    ctx.strokeStyle = stripeColor;
    ctx.lineWidth = 2.5;
    ctx.beginPath();
    ctx.moveTo(0, 12);
    ctx.lineTo(12, 0);
    ctx.stroke();

    ctx.beginPath();
    ctx.moveTo(-3, 3);
    ctx.lineTo(3, -3);
    ctx.stroke();

    ctx.beginPath();
    ctx.moveTo(9, 15);
    ctx.lineTo(15, 9);
    ctx.stroke();

    return ctx.createPattern(canvas, 'repeat') || stripeColor;
  } catch {
    return stripeColor;
  }
}
