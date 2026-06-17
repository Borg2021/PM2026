<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { FileTemplateItem } from '@/types/template'
import {
  getTemplateDetail,
  createTemplate,
  updateTemplate,
  saveTemplateFiles
} from '@/api/template'
import FileTable from './FileTable.vue'

const route = useRoute()
const router = useRouter()

const routeName = computed(() => (route.name as string) || '')

const mode = computed<'create' | 'edit' | 'view'>(() => {
  if (routeName.value === 'FileTemplateCreate') return 'create'
  if (routeName.value === 'FileTemplateEdit') return 'edit'
  return 'view'
})

const isView = computed(() => mode.value === 'view')
const isEdit = computed(() => mode.value !== 'view')

const templateId = computed<number | undefined>(() => {
  const paramsId = route.params.id as string | undefined
  if (paramsId) return Number(paramsId)
  const queryId = route.query.templateId as string | undefined
  if (queryId) return Number(queryId)
  return undefined
})

const form = ref({
  templateCode: '',
  templateName: '',
  description: ''
})

const files = ref<FileTemplateItem[]>([])
const loading = ref(false)
const saving = ref(false)

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
    files.value = detail.fileItems || []
  } catch {
    // 错误已在请求拦截器处理
  } finally {
    loading.value = false
  }
}

onMounted(loadDetail)

async function handleSave() {
  if (!form.value.templateName.trim()) {
    ElMessage.warning('请输入模板名称')
    return
  }

  const emptyFile = files.value.find(f => !f.fileName?.trim())
  if (emptyFile) {
    ElMessage.warning('文件清单中的文件名称不能为空')
    return
  }

  saving.value = true
  try {
    let id = templateId.value

    if (!id) {
      const code =
        form.value.templateCode.trim() || `F${Date.now()}`
      const res = await createTemplate({
        templateCode: code,
        templateName: form.value.templateName.trim(),
        templateType: 4,
        description: form.value.description.trim()
      })
      id = res.data.id
    } else {
      await updateTemplate(id, {
        templateName: form.value.templateName.trim(),
        description: form.value.description.trim()
      })
    }

    // 清理临时属性后提交
    const cleanFiles = files.value.map(f => ({
      sortOrder: f.sortOrder,
      fileName: f.fileName,
      required: f.required,
      isPublic: f.isPublic,
      viewRoles: f.viewRoles,
      deptId: f.deptId,
      deptName: f.deptName,
      remark: f.remark
    }))
    await saveTemplateFiles(id, cleanFiles)
    ElMessage.success('保存成功')
    router.push('/template/list')
  } catch {
    // 错误已在拦截器处理
  } finally {
    saving.value = false
  }
}

function handleCancel() {
  router.push('/template/list')
}

function handleReturn() {
  router.push('/template/list')
}
</script>

<template>
  <div class="file-editor" v-loading="loading">
    <!-- 基本信息 -->
    <el-card class="info-card" shadow="never">
      <template #header>
        <span>
          {{
            mode === 'create'
              ? '新建文件模板'
              : mode === 'edit'
                ? '编辑文件模板'
                : '查看文件模板'
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

    <!-- 文件清单表格 -->
    <el-card class="table-card" shadow="never">
      <template #header><span>文件清单</span></template>
      <FileTable v-model="files" :readonly="isView" />
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
.file-editor {
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
