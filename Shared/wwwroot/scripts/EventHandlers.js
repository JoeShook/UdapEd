window.pageEventHandlers = {
  onFocus: function () {
    window.DotNet.invokeMethodAsync('UdapEd.Shared', 'OnPageFocus');
  },
  onBlur: function () {
    window.DotNet.invokeMethodAsync('UdapEd.Shared', 'OnPageBlur');
  },
  registerHandlers: function () {
    window.addEventListener('focus', this.onFocus);
    window.addEventListener('blur', this.onBlur);
  },
  unregisterHandlers: function () {
    window.removeEventListener('focus', this.onFocus);
    window.removeEventListener('blur', this.onBlur);
  }
};