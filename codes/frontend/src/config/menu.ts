export interface MenuItem {
  path: string
  title: string
  icon?: string
  roles?: string[]
  children?: MenuItem[]
}

const menuConfig: MenuItem[] = [
  {
    path: '/workbench',
    title: '我的工作台',
    icon: 'Monitor',
    roles: ['admin', 'templateAdmin', 'user']
  },
  {
    path: '/system',
    title: '系统管理',
    icon: 'Setting',
    roles: ['admin'],
    children: [
      { path: '/system/users', title: '人员管理', icon: 'User', roles: ['admin'] },
      { path: '/system/departments', title: '部门管理', icon: 'OfficeBuilding', roles: ['admin'] },
      { path: '/system/functions', title: '职能管理', icon: 'Stamp', roles: ['admin'] },
      { path: '/system/permissions', title: '权限管理', icon: 'Lock', roles: ['admin'] },
      {
        path: '/system/config',
        title: '模板配置',
        icon: 'Tools',
        roles: ['admin', 'templateAdmin'],
        children: [
          { path: '/template/list', title: '模板管理', icon: 'Document', roles: ['admin', 'templateAdmin'] },
          { path: '/template/bundles', title: '计划模板集', icon: 'Collection', roles: ['admin', 'templateAdmin'] }
        ]
      },
      {
        path: '/system/param-config',
        title: '参数配置',
        icon: 'Operation',
        roles: ['admin'],
        children: [
          { path: '/system/dict-types', title: '字典类型管理', icon: 'List', roles: ['admin'] },
          { path: '/system/dicts', title: '字典管理', icon: 'Reading', roles: ['admin'] },
          { path: '/system/sys-params', title: '系统参数', icon: 'Setting', roles: ['admin'] }
        ]
      }
    ]
  },
  {
    path: '/project',
    title: '项目管理',
    icon: 'Folder',
    roles: ['admin', 'templateAdmin'],
    children: [
      { path: '/project/list', title: '项目管理', icon: 'Document', roles: ['admin', 'templateAdmin'] },
      { path: '/project/tasks', title: '任务管理', icon: 'List', roles: ['admin', 'templateAdmin'] },
      { path: '/project/issues', title: '问题管理', icon: 'WarningFilled', roles: ['admin', 'templateAdmin'] },
      { path: '/project/files', title: '文件管理', icon: 'FolderOpened', roles: ['admin', 'templateAdmin'] }
    ]
  }
]

export default menuConfig
