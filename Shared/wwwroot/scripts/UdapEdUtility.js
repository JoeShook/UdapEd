function downloadFileFromBytes(bytes, mimeType, fileName) {
  const blob = new Blob([new Uint8Array(bytes)], { type: mimeType });
  const link = document.createElement('a');
  link.href = URL.createObjectURL(blob);
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
}

function toggleText() {
  const expandableText = document.getElementById('expandableText');
  const indicator = document.getElementById('indicator');
  if (expandableText.classList.contains('expanded')) {
    expandableText.classList.remove('expanded');
    indicator.textContent = '[+]';
  }
  else {
    expandableText.classList.add('expanded');
    indicator.textContent = '[-]';
  }
}
