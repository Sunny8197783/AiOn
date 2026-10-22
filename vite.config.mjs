import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Rooted at src/ so the dependency scan doesn't crawl safe-route's own HTML pages.
// .mjs rather than "type": "module", which would flip .claude/helpers/*.js to ESM.
export default defineConfig({ root: 'src', plugins: [react()] })
