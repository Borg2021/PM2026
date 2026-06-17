import router from '@/router'
import { ElMessage } from 'element-plus'
import { isNavigationFailure } from 'vue-router'

/** 打开项目查看页（工作台 / 项目列表共用） */
export async function openProjectView(projectId: number | undefined | null): Promise<boolean> {
  const id = Number(projectId)
  if (!projectId || !Number.isFinite(id) || id <= 0) {
    ElMessage.warning('无法打开项目：ID 无效')
    return false
  }

  const target = `/project/view/${id}`
  const failure = await router.push(target).catch((err) => err)

  if (isNavigationFailure(failure)) {
    ElMessage.warning('无法打开该项目，请检查权限或刷新后重试')
    return false
  }
  return true
}
