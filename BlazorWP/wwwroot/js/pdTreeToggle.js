function setupPDTreeToggle() {
  document.addEventListener('click', function (e) {
    const target = e.target;
    const header = target.closest('.pdtreenode_header');
    if (!header) return;
    const toggler = header.querySelector('.fa-plus-square, .fa-minus-square');
    if (!toggler) return;
    const isIcon = target.matches('i') && !target.classList.contains('fa-plus-square') && !target.classList.contains('fa-minus-square');
    const isSpan = target.matches('.pdtreenode_content > span');
    if (isIcon || isSpan) {
      toggler.click();
    }
  });

  document.querySelectorAll('.pdtreenode_header .fa-plus-square, .pdtreenode_header .fa-minus-square').forEach(el => {
    el.style.display = 'none';
  });
}

document.addEventListener('DOMContentLoaded', setupPDTreeToggle);
