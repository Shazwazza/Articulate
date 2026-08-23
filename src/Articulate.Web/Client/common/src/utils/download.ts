export const getDownloadFileName = (contentDisposition: string | null | undefined, fallback: string): string => {
  if (!contentDisposition) return fallback;

  const encoded = contentDisposition.match(/filename\*="UTF-8''([^"]+)"/);
  if (encoded?.[1]) return encoded[1];

  const plain = contentDisposition.match(/filename="?([^"]+)"?/);
  return plain?.[1] || fallback;
};

export const downloadBlob = (blob: Blob, fileName: string): void => {
  const url = window.URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.style.display = 'none';
  anchor.href = url;
  anchor.download = fileName;
  document.body.appendChild(anchor);
  // Keep this click out of the Umbraco backoffice router.
  anchor.dispatchEvent(new MouseEvent('click', { bubbles: false, cancelable: true, composed: false, view: window }));
  window.setTimeout(() => {
    window.URL.revokeObjectURL(url);
    anchor.remove();
  }, 1000);
};
