<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { createTasksFromTemplate } from '@/api/project'
import { getTemplateList } from '@/api/template'
import type { Template } from '@/types/template'

const props = defineProps<{
  visible: boolean
  projectId: number | null
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  imported: [count: number]
}>()

const templateList = ref<Template[]>([])
const templateLoading = ref(false)
const selectedTemplateId = ref<number | null>(null)
const templateSearch = ref('')

// 关闭时重置状态
watch(() => props.visible, (val) => {
  if (!val) {
    templateSearch.value = ''
    selectedTemplateId.value = null
  }
})

const filteredTemplates = computed(() => {
  if (!templateSearch.value) return templateList.value
  const kw = templateSearch.value.toLowerCase()
  return templateList.value.filter(t =>
    t.templateName.toLowerCase().includes(kw) ||
    t.templateCode.toLowerCase().includes(kw)
  )
})

async function open() {
  templateLoading.value = true
  selectedTemplateId.value = null
  templateSearch.value = ''
  try {
    const res = await getTemplateList({ pageIndex: 1, pageSize: 200, templateType: 1 })
    templateList.value = res.data.items
  } catch { /* 拦截器统一处理 */ }
  finally { templateLoading.value = false }
}

async function handleCreateFromTemplate() {
  if (!props.projectId || !selectedTemplateId.value) {
    ElMessage.warning('请选择一个模板')
    return
  }
  try {
    await ElMessageBox.confirm(
      '从模板新增将清空当前所有任务计划数据，确定继续吗？',
      '提示',
      { confirmButtonText: '确定', cancelButtonText: '取消', type: 'warning' }
    )
  } catch {
    return
  }
  templateLoading.value = true
  try {
    const res = await createTasksFromTemplate(props.projectId, selectedTemplateId.value)
    ElMessage.success(`成功创建 ${res.data.count} 条任务`)
    emit('imported', res.data.count)
    emit('update:visible', false)
  } catch { /* 拦截器统一处理 */ }
  finally { templateLoading.value = false }
}

// 父组件通过 ref 调用 open()
defineExpose({ open })
</script>

<template>
  <el-dialog :model-value="visible" title="从模板新增任务" width="832px" :close-on-click-modal="false" @update:model-value="emit('update:visible', $event)">
    <el-input v-model="templateSearch" placeholder="搜索模板编号或名称..." clearable style="margin-bottom:12px" />
    <el-table
      :data="filteredTemplates"
      border
      size="small"
      style="width:100%"
      max-height="480"
      highlight-current-row
      @current-change="(row: Template | null) => selectedTemplateId = row?.id ?? null"
    >
      <el-table-column width="50" align="center">
        <template #default="{ row }">
          <el-radio :model-value="selectedTemplateId" :value="row.id" @change="selectedTemplateId = row.id">
            <span></span>
          </el-radio>
        </template>
      </el-table-column>
      <el-table-column prop="templateCode" label="模板编号" width="185" />
      <el-table-column prop="templateName" label="模板名称" min-width="180" show-overflow-tooltip />
      <el-table-column prop="description" label="描述" min-width="160" show-overflow-tooltip />
    </el-table>
    <template #footer>
      <el-button @click="emit('update:visible', false)">取消</el-button>
      <el-button type="danger" :loading="templateLoading" @click="handleCreateFromTemplate">确定</el-button>
    </template>
  </el-dialog>
</template>
