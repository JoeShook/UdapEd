function downloadFileFromBytes(bytes, mimeType, fileName) {
  const blob = new Blob([new Uint8Array(bytes)], { type: mimeType });
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob);
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
}