var UdapEd = UdapEd || {};
UdapEd.setFocus = function (id) {

  let e = document.getElementById(id);
  if (e != null) {
    e.click();
  }
};

UdapEd.scrollTo = function (id) {
  let e = document.getElementById(id);
  if (e != null) {
    e.scrollIntoView({ behavior: 'smooth' });
    return true;
  }
  return false;
};
