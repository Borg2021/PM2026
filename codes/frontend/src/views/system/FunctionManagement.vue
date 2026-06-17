<script setup lang="ts">
import { ref, shallowRef, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getFunctionList, createFunction, updateFunction, deleteFunction } from '@/api/system'
import type { FunctionItem } from '@/types/system'

const tableData = shallowRef<FunctionItem[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)

const form = reactive({ code: '', name: '', description: '', sortOrder: 0 })
const formRules = {
  code: [{ required: true, message: '请输入职能编号', trigger: 'blur' }],
  name: [{ required: true, message: '请输入职能名称', trigger: 'blur' }]
}
const formRef = ref()

function fetchData() {
  loading.value = true
  getFunctionList()
    .then(res => { tableData.value = res.data })
    .finally(() => { loading.value = false })
}

function handleCreate() {
  editingId.value = null
  form.code = ''
  form.name = ''
  form.description = ''
  form.sortOrder = 0
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

function handleEdit(row: FunctionItem) {
  editingId.value = row.id
  form.code = row.code
  form.name = row.name
  form.description = row.description || ''
  form.sortOrder = row.sortOrder
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

async function handleDelete(row: FunctionItem) {
  try {
    await ElMessageBox.confirm(`确定要删除职能「${row.name}」吗？`, '确认删除', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
    await deleteFunction(row.id)
    ElMessage.success('删除成功')
    fetchData()
  } catch { /* 取消 */ }
}

async function submit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  if (editingId.value) {
    await updateFunction(editingId.value, {
      code: form.code.trim(),
      name: form.name.trim(),
      description: form.description.trim() || undefined,
      sortOrder: form.sortOrder
    })
    ElMessage.success('更新成功')
  } else {
    await createFunction({
      code: form.code.trim(),
      name: form.name.trim(),
      description: form.description.trim() || undefined,
      sortOrder: form.sortOrder
    })
    ElMessage.success('创建成功')
  }
  dialogVisible.value = false
  fetchData()
}

onMounted(() => { fetchData() })
</script>

<template>
  <div class="page-container">
    <h2>职能管理</h2>

    <div class="toolbar">
      <div />
      <el-button type="danger" @click="handleCreate">+ 新建职能</el-button>
    </div>

    <el-table :data="tableData" v-loading="loading" stripe border style="width: 100%">
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column prop="code" label="职能编号" width="182" show-overflow-tooltip />
      <el-table-column prop="name" label="职能名称" width="210" show-overflow-tooltip />
      <el-table-column prop="description" label="职能说明" min-width="200" show-overflow-tooltip>
        <template #default="{ row }">
          {{ row.description || '—' }}
        </template>
      </el-table-column>
      <el-table-column prop="sortOrder" label="排序" width="80" />
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
      :title="editingId ? '编辑职能' : '新建职能'"
      width="460px"
      :close-on-click-modal="false"
    >
      <el-form ref="formRef" :model="form" :rules="formRules" label-width="80px">
        <el-form-item label="职能编号" prop="code">
          <el-input v-model="form.code" placeholder="唯一编码，如 dev" maxlength="50" />
        </el-form-item>
        <el-form-item label="职能名称" prop="name">
          <el-input v-model="form.name" placeholder="如 开发工程师" maxlength="50" />
        </el-form-item>
        <el-form-item label="职能说明">
          <el-input v-model="form.description" type="textarea" placeholder="职能描述（选填）" maxlength="200" :rows="3" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="form.sortOrder" :min="0" :max="9999" />
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
