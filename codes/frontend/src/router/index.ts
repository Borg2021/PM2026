import { createRouter, createWebHashHistory } from 'vue-router'
import { ElMessage } from 'element-plus'

const router = createRouter({
  history: createWebHashHistory(),
  routes: [
    {
      path: '/login',
      name: 'Login',
      component: () => import('@/views/Login.vue')
    },
    {
      path: '/',
      component: () => import('@/layouts/MainLayout.vue'),
      redirect: '/workbench',
      children: [
        /* ---- 我的工作台 ---- */
        {
          path: 'workbench',
          name: 'Workbench',
          component: () => import('@/views/Workbench.vue')
        },
        /* ---- 系统管理 ---- */
        {
          path: 'system/users',
          name: 'UserManagement',
          component: () => import('@/views/system/UserManagement.vue')
        },
        {
          path: 'system/permissions',
          name: 'PermissionManagement',
          component: () => import('@/views/system/PermissionManagement.vue')
        },
        {
          path: 'system/departments',
          name: 'DepartmentManagement',
          component: () => import('@/views/system/DepartmentManagement.vue')
        },
        {
          path: 'system/functions',
          name: 'FunctionManagement',
          component: () => import('@/views/system/FunctionManagement.vue')
        },
        {
          path: 'system/dict-types',
          name: 'DictTypeManagement',
          component: () => import('@/views/system/DictTypeManagement.vue')
        },
        {
          path: 'system/dicts',
          name: 'DictManagement',
          component: () => import('@/views/system/DictManagement.vue')
        },
        {
          path: 'system/sys-params',
          name: 'SysParamManagement',
          component: () => import('@/views/system/SysParamManagement.vue')
        },
        {
          path: 'system/config',
          name: 'ConfigCenter',
          component: () => import('@/views/project/ConfigCenter.vue')
        },
        /* ---- 系统设置 ---- */
        {
          path: 'system/settings',
          name: 'UserSettings',
          component: () => import('@/views/system/UserSettings.vue')
        },
        /* ---- 模板管理 ---- */
        {
          path: 'template/list',
          name: 'TemplateList',
          component: () => import('@/views/template/TemplateList.vue')
        },
        {
          path: 'template/plan/create',
          name: 'PlanCreate',
          component: () => import('@/views/template/components/plan/PlanTemplateEditor.vue')
        },
        {
          path: 'template/plan/edit/:id',
          name: 'PlanEdit',
          component: () => import('@/views/template/components/plan/PlanTemplateEditor.vue')
        },
        {
          path: 'template/plan/view/:id',
          name: 'PlanView',
          component: () => import('@/views/template/components/plan/PlanTemplateEditor.vue')
        },
        {
          path: 'template/milestone/create',
          name: 'MilestoneCreate',
          component: () => import('@/views/template/components/milestone/MilestoneTemplateEditor.vue')
        },
        {
          path: 'template/milestone/edit/:id',
          name: 'MilestoneEdit',
          component: () => import('@/views/template/components/milestone/MilestoneTemplateEditor.vue')
        },
        {
          path: 'template/milestone/view/:id',
          name: 'MilestoneView',
          component: () => import('@/views/template/components/milestone/MilestoneTemplateEditor.vue')
        },
        {
          path: 'template/member/create',
          name: 'MemberCreate',
          component: () => import('@/views/template/components/member/MemberTemplateEditor.vue')
        },
        {
          path: 'template/member/edit/:id',
          name: 'MemberEdit',
          component: () => import('@/views/template/components/member/MemberTemplateEditor.vue')
        },
        {
          path: 'template/member/view/:id',
          name: 'MemberView',
          component: () => import('@/views/template/components/member/MemberTemplateEditor.vue')
        },
        {
          path: 'template/file/create',
          name: 'FileTemplateCreate',
          component: () => import('@/views/template/components/file/FileTemplateEditor.vue')
        },
        {
          path: 'template/file/edit/:id',
          name: 'FileTemplateEdit',
          component: () => import('@/views/template/components/file/FileTemplateEditor.vue')
        },
        {
          path: 'template/file/view/:id',
          name: 'FileTemplateView',
          component: () => import('@/views/template/components/file/FileTemplateEditor.vue')
        },
        /* ---- 计划模板集 ---- */
        {
          path: 'template/bundles',
          name: 'PlanBundleList',
          component: () => import('@/views/template/PlanBundleList.vue')
        },
        {
          path: 'template/bundles/create',
          name: 'PlanBundleCreate',
          component: () => import('@/views/template/PlanBundleEditor.vue')
        },
        {
          path: 'template/bundles/edit/:id',
          name: 'PlanBundleEdit',
          component: () => import('@/views/template/PlanBundleEditor.vue')
        },
        /* ---- 项目管理 ---- */
        {
          path: 'project/list',
          name: 'ProjectList',
          component: () => import('@/views/project/ProjectList.vue')
        },
        {
          path: 'project/tasks',
          name: 'TaskManagement',
          component: () => import('@/views/project/TaskManagement.vue')
        },
        {
          path: 'project/files',
          name: 'ProjectFiles',
          component: () => import('@/views/project/ProjectFiles.vue')
        },
        {
          path: 'project/create',
          name: 'ProjectCreate',
          component: () => import('@/views/project/ProjectEditor.vue')
        },
        {
          path: 'project/edit/:id',
          name: 'ProjectEdit',
          component: () => import('@/views/project/ProjectEditor.vue')
        },
        {
          path: 'project/view/:id',
          name: 'ProjectView',
          component: () => import('@/views/project/ProjectEditor.vue')
        },
        /* ---- 根路径重定向 ---- */
        {
          path: '',
          redirect: '/workbench'
        }
      ]
    },
  ]
})

router.beforeEach(async (to) => {
  const token = localStorage.getItem('token')
  if (to.path !== '/login' && !token) {
    return '/login'
  }

  if (to.path.startsWith('/project/')) {
    const { useAuthStore } = await import('@/store/auth')
    const auth = useAuthStore()
    if (!auth.permissions.length) await auth.fetchPermissionsAndMenus()

    if (to.path === '/project/list' && !auth.hasPermission('project:list:view')) {
      ElMessage.warning('无权限访问项目列表')
      return '/workbench'
    }
    // 项目查看：登录即可进入，能否看到数据由详情 API / 后端数据权限判定
    if (to.path.includes('/project/view/')) {
      const id = Number(to.params.id)
      if (!to.params.id || !Number.isFinite(id) || id <= 0) {
        ElMessage.warning('项目链接无效')
        return '/workbench'
      }
    }
    if (to.path.includes('/project/edit/') && !auth.hasPermission('project:edit')) {
      ElMessage.warning('无权限编辑项目')
      return '/workbench'
    }
    if (to.path === '/project/create' && !auth.hasPermission('project:create')) {
      ElMessage.warning('无权限新建项目')
      return '/workbench'
    }
  }
})

export default router
