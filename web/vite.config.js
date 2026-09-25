import { defineConfig } from 'vite'

// araatti.site 는 nginx 가 /var/www/araatti/ 를 그대로 내보내는 정적 사이트다.
// 빌드 결과(dist/)가 그 폴더에 그대로 올라간다. scripts/deploy.mjs 참고.
//
// React 등을 붙이면 여기에 plugins 를 더하면 된다. 예:
//   import react from '@vitejs/plugin-react'
//   export default defineConfig({ plugins: [react()], ... })
export default defineConfig({
  // 사이트가 도메인 맨 앞(https://araatti.site/)에 있다. 하위 경로가 아니다.
  base: '/',

  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
})
