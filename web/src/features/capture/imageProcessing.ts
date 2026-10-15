/**
 * Image processing utilities for floor capture PWA:
 * - Resizes images to max 1600px dimension
 * - Compresses to WebP / JPEG targeting < 400 KB
 * - Strips EXIF metadata (including GPS location) by re-rendering to Canvas
 * - Generates client GUID idempotency keys
 */

export interface ProcessedImageResult {
  blob: Blob;
  fileName: string;
  fileSizeBytes: number;
  width: number;
  height: number;
  idempotencyKey: string;
}

export function generateClientGuid(): string {
  if (typeof crypto !== 'undefined' && crypto.randomUUID) {
    return crypto.randomUUID();
  }
  // Fallback UUID v4 generator
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    const v = c === 'x' ? r : (r & 0x3) | 0x8;
    return v.toString(16);
  });
}

export async function processAndCompressImage(
  file: File | Blob,
  originalFileName: string = 'capture.jpg',
  maxDimension: number = 1600,
  targetMaxSizeBytes: number = 400 * 1024 // 400 KB
): Promise<ProcessedImageResult> {
  const idempotencyKey = generateClientGuid();
  const rawFileName = (file instanceof File ? file.name : null) || originalFileName;
  const isPdf =
    file.type === 'application/pdf' ||
    rawFileName.toLowerCase().endsWith('.pdf');

  // If PDF, bypass canvas processing (which fails with HTMLImageElement) and return raw PDF
  if (isPdf) {
    return {
      blob: file,
      fileName: rawFileName,
      fileSizeBytes: file.size,
      width: 0,
      height: 0,
      idempotencyKey,
    };
  }

  try {
    // Load image into HTMLImageElement
    const imageBitmapOrImg = await createImageElement(file);
    const { width: originalWidth, height: originalHeight } = imageBitmapOrImg;

    // Calculate scaled dimensions
    let targetWidth = originalWidth;
    let targetHeight = originalHeight;

    if (targetWidth > maxDimension || targetHeight > maxDimension) {
      if (targetWidth > targetHeight) {
        targetHeight = Math.round((targetHeight * maxDimension) / targetWidth);
        targetWidth = maxDimension;
      } else {
        targetWidth = Math.round((targetWidth * maxDimension) / targetHeight);
        targetHeight = maxDimension;
      }
    }

    // Draw to offscreen canvas (this naturally strips all EXIF tags including GPS)
    const canvas = document.createElement('canvas');
    canvas.width = targetWidth;
    canvas.height = targetHeight;
    const ctx = canvas.getContext('2d');

    if (!ctx) {
      throw new Error('Unable to create canvas rendering context');
    }

    ctx.drawImage(imageBitmapOrImg, 0, 0, targetWidth, targetHeight);

    // Compress iteratively to stay under targetMaxSizeBytes
    let quality = 0.85;
    let mimeType = 'image/webp';
    let blob = await canvasToBlob(canvas, mimeType, quality);

    // Fallback to JPEG if browser doesn't support WebP export
    if (!blob || blob.type !== 'image/webp') {
      mimeType = 'image/jpeg';
      blob = await canvasToBlob(canvas, mimeType, quality);
    }

    // Iterative quality reduction if over 400 KB budget
    while (blob && blob.size > targetMaxSizeBytes && quality > 0.4) {
      quality -= 0.15;
      blob = await canvasToBlob(canvas, mimeType, quality);
    }

    if (!blob) {
      throw new Error('Image compression failed');
    }

    const cleanExtension = mimeType === 'image/webp' ? '.webp' : '.jpg';
    const baseName = rawFileName.replace(/\.[^/.]+$/, '');
    const outFileName = `${baseName}_compressed${cleanExtension}`;

    return {
      blob,
      fileName: outFileName,
      fileSizeBytes: blob.size,
      width: targetWidth,
      height: targetHeight,
      idempotencyKey,
    };
  } catch (err) {
    console.warn('Canvas processing unavailable or failed; uploading raw file:', err);
    return {
      blob: file,
      fileName: rawFileName,
      fileSizeBytes: file.size,
      width: 0,
      height: 0,
      idempotencyKey,
    };
  }
}

function createImageElement(file: File | Blob): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    const url = URL.createObjectURL(file);
    img.onload = () => {
      URL.revokeObjectURL(url);
      resolve(img);
    };
    img.onerror = (err) => {
      URL.revokeObjectURL(url);
      reject(err);
    };
    img.src = url;
  });
}

function canvasToBlob(
  canvas: HTMLCanvasElement,
  mimeType: string,
  quality: number
): Promise<Blob | null> {
  return new Promise((resolve) => {
    canvas.toBlob((blob) => resolve(blob), mimeType, quality);
  });
}
