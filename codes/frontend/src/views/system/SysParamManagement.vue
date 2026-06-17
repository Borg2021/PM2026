<script setup lang="ts">
import { ref, shallowRef, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getSysParamList, createSysParam, updateSysParam, deleteSysParam } from '@/api/system'
import type { SysParamItem } from '@/types/system'

const tableData = shallowRef<SysParamItem[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref()

const form = reactive({
  paramKey: '',
  paramValue: '',
  description: ''
})

const formRules = {
  paramKey: [{ required: true, message: '请输入参数键', trigger: 'blur' }],
  paramValue: [{ required: true, message: '请输入参数值', trigger: 'blur' }]
}

async function fetchData() {
  loading.value = true
  try {
    const res = await getSysParamList()
    tableData.value = res.data
  } finally {
    loading.value = false
  }
}

function handleCreate() {
  editingId.value = null
  form.paramKey = ''
  form.paramValue = ''
  form.description = ''
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

function handleEdit(row: SysParamItem) {
  editingId.value = row.id
  form.paramKey = row.paramKey
  form.paramValue = row.paramValue
  form.description = row.description || ''
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

async function handleDelete(row: SysParamItem) {
  try {
    await ElMessageBox.confirm(`确定要删除参数「${row.paramKey}」吗？`, '确认删除', {
      confirmButtonText: '确定', cancelButtonText: '取消', type: 'warning'
    })
    await deleteSysParam(row.id)
    ElMessage.success('删除成功')
    fetchData()
  } catch { /* 取消或错误 */ }
}

async function submit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  const data = {
    paramKey: form.paramKey.trim(),
    paramValue: form.paramValue.trim(),
    description: form.description.trim() || undefined
  }

  if (editingId.value) {
    await updateSysParam(editingId.value, data)
    ElMessage.success('更新成功')
  } else {
    await createSysParam(data)
    ElMessage.success('创建成功')
  }
  dialogVisible.value = false
  fetchData()
}

onMounted(() => { fetchData() })
</script>

<template>
  <div class="page-container">
    <h2>系统参数</h2>

    <div class="toolbar">
      <div />
      <el-button type="danger" @click="handleCreate">+ 新建参数</el-button>
    </div>

    <el-table :data="tableData" v-loading="loading" stripe border style="width: 100%">
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column prop="paramKey" label="参数键" width="200" show-overflow-tooltip />
      <el-table-column prop="paramValue" label="参数值" width="200" show-overflow-tooltip />
      <el-table-column prop="description" label="说明" min-width="200" show-overflow-tooltip />
      <el-table-column label="操作" width="160" fixed="right">
        <template #default="{ row }">
          <el-button link style="color: #67c23a" @click="handleEdit(row)">编辑</el-button>
          <el-button link style="color: #f56c6c" @click="handleDelete(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog
      :model-value="dialogVisible"
      @update:model-value="dialogVisible = $event"
      :title="editingId ? '编辑参数' : '新建参数'"
      width="520px"
      :close-on-click-modal="false"
    >
      <el-form ref="formRef" :model="form" :rules="formRules" label-width="100px">
        <el-form-item label="参数键" prop="paramKey">
          <el-input v-model="form.paramKey" placeholder="如 plan_code_rule" maxlength="50" />
        </el-form-item>
        <el-form-item label="参数值" prop="paramValue">
          <el-input v-model="form.paramValue" placeholder="如 3,2,2" maxlength="100" />
        </el-form-item>
        <el-form-item label="说明" prop="description">
          <el-input v-model="form.description" placeholder="参数用途说明" maxlength="200" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="danger" @click="submit">确定</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page-container {
  padding: 24px;
}
.page-container h2 {
  margin: 0 0 20px;
  font-size: 20px;
  font-weight: 600;
  color: #303133;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
</style>
