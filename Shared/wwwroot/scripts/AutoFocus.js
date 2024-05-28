var UdapEd = UdapEd || {};
UdapEd.setFocus = function (id) {

  let e = document.getElementById(id);
  if (e != null) {
    e.click();
  }
};