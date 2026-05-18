/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_AUTHORITY: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
