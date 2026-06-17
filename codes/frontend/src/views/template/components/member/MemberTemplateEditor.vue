<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { TemplateMember } from '@/types/template'
import {
  getTemplateDetail,
  createTemplate,
  updateTemplate,
  saveMembers
} from '@/api/template'
import MemberTable from './MemberTable.vue'

const route = useRoute()
const router = useRouter()

// ========== 路由模式判定 ==========
const routeName = computed(() => (route.name as string) || '')

const mode = computed<'create' | 'edit' | 'view'>(() => {
  if (routeName.value === 'MemberCreate') return 'create'
  if (routeName.value === 'MemberEdit') return 'edit'
  return 'view'
})

const isView = computed(() => mode.value === 'view')
const isEdit = computed(() => mode.value !== 'view')

// ========== 模板 ID（路由 param 或 query） ==========
const templateId = computed<number | undefined>(() => {
  const paramsId = route.params.id as string | undefined
  if (paramsId) return Number(paramsId)
  const queryId = route.query.templateId as string | undefined
  if (queryId) return Number(queryId)
  return undefined
})

// ========== 表单数据 ==========
const form = ref({
  templateCode: '',
  templateName: '',
  description: ''
})

const members = ref<TemplateMember[]>([])
const loading = ref(false)
const saving = ref(false)

// ========== 加载模板详情 ==========
async function loadDetail() {
  if (!templateId.value) return
  loading.value = true
  try {
    const res = await getTemplateDetail(templateId.value)
    const detail = res.data
    form.value = {
      templateCode: detail.templateCode,
      templateName: detail.templateName,
      description: detail.description || ''
    }
    members.value = detail.members || []
  } catch {
    // 错误已在请求拦截器处理
  } finally {
    loading.value = false
  }
}

onMounted(loadDetail)

// ========== 保存 ==========
async function handleSave() {
  if (!form.value.templateName.trim()) {
    ElMessage.warning('请输入模板名称')
    return
  }

  saving.value = true
  try {
    let id = templateId.value

    if (!id) {
      // ---- 创建模式 ----
      const code =
        form.value.templateCode.trim() || `M${Date.now()}`
      const res = await createTemplate({
        templateCode: code,
        templateName: form.value.templateName.trim(),
        templateType: 3,
        description: form.value.description.trim()
      })
      id = res.data.id
    } else {
      // ---- 编辑模式 ----
      await updateTemplate(id, {
        templateName: form.value.templateName.trim(),
        description: form.value.description.trim()
      })
    }

    await saveMembers(id, members.value)
    ElMessage.success('保存成功')
    router.push('/template/list')
  } catch {
    // 错误已在拦截器处理
  } finally {
    saving.value = false
  }
}

// ========== 导航 ==========
function handleCancel() {
  router.push('/template/list')
}

function handleReturn() {
  router.push('/template/list')
}
</script>

<template>
  <div class="member-editor" v-loading="loading">
    <!-- 基本信息 -->
    <el-card class="info-card" shadow="never">
      <template #header>
        <span>
          {{
            mode === 'create'
              ? '新建成员模板'
              : mode === 'edit'
                ? '编辑成员模板'
                : '查看成员模板'
          }}
        </span>
      </template>

      <el-form label-width="100px" size="default">
        <el-form-item label="模板编号">
          <el-input
            v-model="form.templateCode"
            disabled
            placeholder="系统自动生成"
          />
        </el-form-item>

        <el-form-item label="模板名称">
          <el-input
            v-model="form.templateName"
            :readonly="isView"
            :disabled="isView"
            placeholder="请输入模板名称"
            maxlength="100"
            show-word-limit
          />
        </el-form-item>

        <el-form-item label="模板描述">
          <el-input
            v-model="form.description"
            :readonly="isView"
            :disabled="isView"
            type="textarea"
            :rows="3"
            placeholder="请输入模板描述"
            maxlength="500"
            show-word-limit
          />
        </el-form-item>
      </el-form>
    </el-card>

    <!-- 成员表格 -->
    <el-card class="table-card" shadow="never">
      <template #header><span>成员配置</span></template>
      <MemberTable v-model="members" :readonly="isView" />
    </el-card>

    <!-- 底部操作栏 -->
    <div class="form-actions">
      <template v-if="isEdit">
        <el-button type="danger" :loading="saving" @click="handleSave">
          保存
        </el-button>
        <el-button @click="handleCancel">取消</el-button>
      </template>
      <template v-else>
        <el-button type="primary" @click="handleReturn">返回</el-button>
      </template>
    </div>
  </div>
</template>

<style scoped>
.member-editor {
  max-width: 1200px;
  margin: 0 auto;
  padding: 20px;
}

.info-card {
  margin-bottom: 20px;
}

.table-card {
  margin-bottom: 20px;
}

.form-actions {
  display: flex;
  justify-content: center;
  gap: 12px;
  padding: 16px 0;
}
</style>
