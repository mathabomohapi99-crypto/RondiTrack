// NEW 9.1: the browser's check. A courtesy only: the API checks again, by the file's BYTES.
export const MAX_PROOF_BYTES = 5 * 1024 * 1024; // same as the API: 5 MB
export const ALLOWED_PROOF_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'application/pdf'] as const;

// null = fine, otherwise the sentence to show under the input
export function checkProofFile(file: { size: number; type: string }): string | null {
  if (file.size === 0) return 'That file is empty. Choose a photo or a PDF.';
  if (file.size > MAX_PROOF_BYTES) {
    return `That file is too large (${(file.size / 1024 / 1024).toFixed(1)} MB). The most we accept is 5 MB.`;
  }
  if (!(ALLOWED_PROOF_TYPES as readonly string[]).includes(file.type)) {
    return 'Choose a photo (JPEG, PNG or WebP) or a PDF.';
  }
  return null;
}