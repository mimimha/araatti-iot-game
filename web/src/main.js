// 페이지를 꾸미는 동작 두 가지. 없어도 페이지는 다 보인다.

// 1. 스크롤하면 위쪽 메뉴에 배경을 깐다
const nav = document.getElementById('nav')
const onScroll = () => nav.classList.toggle('is-solid', window.scrollY > 40)
onScroll()
window.addEventListener('scroll', onScroll, { passive: true })

// 2. 화면에 들어온 블록을 살짝 떠오르게 한다.
//    .js 를 붙인 뒤에만 숨기므로 스크립트가 못 돌면 처음부터 다 보인다.
if ('IntersectionObserver' in window) {
  document.documentElement.classList.add('js')
  const io = new IntersectionObserver((entries) => {
    for (const e of entries) {
      if (!e.isIntersecting) continue
      e.target.classList.add('is-in')
      io.unobserve(e.target)
    }
  }, { rootMargin: '0px 0px -10% 0px', threshold: 0.1 })
  document.querySelectorAll('.reveal').forEach((el) => io.observe(el))
}
