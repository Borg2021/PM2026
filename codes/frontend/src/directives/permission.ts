import type { Directive } from 'vue'
import { useAuthStore } from '@/store/auth'

function applyPermission(el: HTMLElement, binding: { value: string | string[] }) {
  const auth = useAuthStore()
  const codes = Array.isArray(binding.value) ? binding.value : [binding.value]
  if (!codes.length) return
  const allowed = auth.hasAnyPermission(codes)
  if (!allowed) {
    el.style.display = 'none'
    el.setAttribute('data-permission-denied', '1')
  } else {
    el.style.removeProperty('display')
    el.removeAttribute('data-permission-denied')
  }
}

export const vPermission: Directive<HTMLElement, string | string[]> = {
  mounted(el, binding) {
    applyPermission(el, binding)
  },
  updated(el, binding) {
    applyPermission(el, binding)
  }
}
