<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '@/store/auth'
import menuConfig, { type MenuItem } from '@/config/menu'
import { ElMessageBox } from 'element-plus'
import SubMenuRenderer from '@/components/SubMenuRenderer.vue'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()

const isCollapse = ref(false)

const pendingFileBadge = computed(() => {
  if (auth.pendingFileTotal === 0) return ''
  return `${auth.pendingFileTotal},${auth.pendingFileRequired}`
})

function toggleCollapse() {
  isCollapse.value = !isCollapse.value
}

function handleLogout() {
  ElMessageBox.confirm('确定要退出登录吗？', '提示', {
    confirmButtonText: '确定',
    cancelButtonText: '取消',
    type: 'warning'
  }).then(() => {
    auth.logout()
    router.push('/login')
  }).catch(() => {})
}

/* 根据角色过滤菜单 */
function filterMenuByRole(items: MenuItem[]): MenuItem[] {
  const role = auth.role
  return items
    .filter(item => {
      if (!item.roles || item.roles.length === 0) return true
      return item.roles.includes(role)
    })
    .map(item => ({
      ...item,
      children: item.children ? filterMenuByRole(item.children) : undefined
    }))
    .filter(item => {
      // 如果有子菜单但全部被过滤掉，则隐藏父菜单
      if (item.children !== undefined && item.children.length === 0) return false
      return true
    })
}

const visibleMenu = computed(() => {
  if (auth.menus.length > 0) return auth.menus
  return filterMenuByRole(menuConfig)
})

/** 注入任务管理/文件管理菜单项的待处理数量角标 */
function injectBadge(items: any[]): any[] {
  return items.map(item => {
    let badge: string | undefined
    if (item.path === '/project/tasks' && auth.pendingTaskCount > 0) {
      badge = String(auth.pendingTaskCount)
    } else if (item.path === '/project/files' && pendingFileBadge.value) {
      badge = pendingFileBadge.value
    }
    return {
      ...item,
      badge,
      children: item.children ? injectBadge(item.children) : undefined
    }
  })
}

const enrichedMenu = computed(() => injectBadge(visibleMenu.value))

/* 当前激活的菜单项 */
const activeMenu = computed(() => {
  const { path } = route
  // 尝试匹配最长的菜单路径
  let bestMatch = path
  // 对于 /template/plan/edit/1 这样的路径，回退到 /template/list
  const allPaths = collectAllPaths(menuConfig)
  for (const p of allPaths) {
    if (path.startsWith(p) && p.length > bestMatch.length) {
      bestMatch = p
    }
  }
  return bestMatch
})

/** 从菜单配置中查找当前路由的面包屑标题 */
function findBreadcrumb(path: string, items: MenuItem[]): { parent?: string; current: string } | null {
  for (const item of items) {
    // 直接匹配
    if (path === item.path) return { current: item.title }
    if (!item.children) continue
    // 匹配一级子菜单
    for (const child of item.children) {
      if (path === child.path) return { parent: item.title, current: child.title }
      if (!child.children) continue
      // 匹配二级子菜单
      for (const gc of child.children) {
        if (path === gc.path) return { parent: child.title, current: gc.title }
      }
    }
  }
  return null
}

const headerTitle = computed(() => {
  const { path } = route
  if (path === '/workbench') return '我的工作台'

  const found = findBreadcrumb(path, menuConfig)
  if (found) {
    return found.parent ? `${found.parent} > ${found.current}` : found.current
  }

  // 动态路由（不在菜单配置中的）按前缀匹配
  if (path.startsWith('/project/')) return '项目管理'
  if (path.startsWith('/template/')) return '系统管理 > 模板配置'
  if (path.startsWith('/system/')) return '系统管理'

  return '项目管理'
})

function collectAllPaths(items: MenuItem[]): string[] {
  const paths: string[] = []
  for (const item of items) {
    paths.push(item.path)
    if (item.children) {
      paths.push(...collectAllPaths(item.children))
    }
  }
  return paths
}

/* 当前展开的子菜单（含嵌套层级） */
function collectOpenPaths(items: MenuItem[], targetPath: string): string[] {
  for (const item of items) {
    if (item.children?.some(c => targetPath.startsWith(c.path))) {
      // 递归检查子级是否也需要展开
      const childPaths = collectOpenPaths(item.children, targetPath)
      return [item.path, ...childPaths]
    }
  }
  return []
}

const defaultOpeneds = computed(() => {
  return collectOpenPaths(enrichedMenu.value, activeMenu.value)
})

function handleMenuSelect(path: string) {
  if (path !== route.path) {
    router.push(path)
  }
}

onMounted(async () => {
  if (auth.menus.length === 0) {
    await auth.fetchPermissionsAndMenus()
  }
  // 获取待处理任务/文件角标统计（存入 store，Workbench 复用）
  if (auth.pendingTaskCount === 0 && auth.pendingFileTotal === 0) {
    await auth.fetchPendingCounts()
  }
})
</script>

<template>
  <div class="main-layout">
    <!-- 侧边栏 -->
    <el-aside :width="isCollapse ? '64px' : '220px'" class="layout-aside">
      <div class="logo-section">
        <span v-show="!isCollapse" class="logo-text">ROBO 项目管理</span>
        <span v-show="isCollapse" class="logo-text-mini">pm</span>
      </div>

      <el-menu
        :default-active="activeMenu"
        :default-openeds="defaultOpeneds"
        :collapse="isCollapse"
        :collapse-transition="false"
        background-color="#304156"
        text-color="#bfcbd9"
        active-text-color="#f56c6c"
        @select="handleMenuSelect"
      >
        <SubMenuRenderer v-for="item in enrichedMenu" :key="item.path" :item="item" />
      </el-menu>
    </el-aside>

    <!-- 主体区域 -->
    <div class="layout-main">
      <!-- 顶部栏 -->
      <el-header class="layout-header">
        <div class="header-left">
          <el-icon
            class="collapse-btn"
            :size="20"
            @click="toggleCollapse"
          >
            <Fold v-if="!isCollapse" />
            <Expand v-else />
          </el-icon>
          <span class="header-title">{{ headerTitle }}</span>
        </div>
        <div class="header-right">
          <span class="user-name">{{ auth.realName || '用户' }}</span>
          <el-button type="danger" text size="small" @click="handleLogout">
            退出登录
          </el-button>
        </div>
      </el-header>

      <!-- 内容区 -->
      <div class="layout-content">
        <router-view v-slot="{ Component, route: r }">
          <component :is="Component" :key="r.fullPath" />
        </router-view>
      </div>
    </div>
  </div>
</template>

<style scoped>
.main-layout {
  display: flex;
  height: 100vh;
  overflow: hidden;
}

.layout-aside {
  background-color: #304156;
  display: flex;
  flex-direction: column;
  transition: width 0.3s;
  overflow: hidden;
}

.logo-section {
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.logo-text {
  color: #fff;
  font-size: 20px;
  font-weight: 700;
  letter-spacing: 2px;
}

.logo-text-mini {
  color: #fff;
  font-size: 18px;
  font-weight: 700;
}

.el-menu {
  border-right: none;
  flex: 1;
  overflow-y: auto;
}

.layout-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.layout-header {
  height: 56px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  background: #fff;
  border-bottom: 1px solid #e6e6e6;
  padding: 0 20px;
  box-shadow: 0 1px 4px rgba(0, 0, 0, 0.06);
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.collapse-btn {
  cursor: pointer;
  color: #606266;
}

.header-title {
  font-size: 16px;
  font-weight: 500;
  color: #303133;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.user-name {
  font-size: 14px;
  color: #606266;
}

.layout-content {
  flex: 1;
  overflow-y: auto;
  background: #f0f2f5;
  display: flex;
  flex-direction: column;
}
</style>
