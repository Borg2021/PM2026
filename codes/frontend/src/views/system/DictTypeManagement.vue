<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getDictTypeList, createDictType, updateDictType, deleteDictType } from '@/api/system'
import type { DictTypeItem } from '@/types/system'

const tableData = ref<DictTypeItem[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)
const formRef = ref()

const form = reactive({
  dictTypeCode: '',
  dictTypeName: '',
  remark: ''
})

const formRules = {
  dictTypeCode: [{ required: true, message: '请输入字典类型编号', trigger: 'blur' }],
  dictTypeName: [{ required: true, message: '请输入字典类型名称', trigger: 'blur' }]
}

async function fetchData() {
  loading.value = true
  try {
    const res = await getDictTypeList()
    tableData.value = res.data
  } finally {
    loading.value = false
  }
}

function handleCreate() {
  editingId.value = null
  form.dictTypeCode = ''
  form.dictTypeName = ''
  form.remark = ''
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

function handleEdit(row: DictTypeItem) {
  editingId.value = row.id
  form.dictTypeCode = row.dictTypeCode
  form.dictTypeName = row.dictTypeName
  form.remark = row.remark || ''
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

async function handleDelete(row: DictTypeItem) {
  try {
    await ElMessageBox.confirm(
      `确定要删除字典类型「${row.dictTypeName}」吗？`,
      '确认删除',
      { confirmButtonText: '确定', cancelButtonText: '取消', type: 'warning' }
    )
    await deleteDictType(row.id)
    ElMessage.success('删除成功')
    fetchData()
  } catch { /* 取消或错误 */ }
}

async function submit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  const data = {
    dictTypeCode: form.dictTypeCode.trim(),
    dictTypeName: form.dictTypeName.trim(),
    remark: form.remark.trim() || undefined
  }

  if (editingId.value) {
    await updateDictType(editingId.value, data)
    ElMessage.success('更新成功')
  } else {
    await createDictType(data)
    ElMessage.success('创建成功')
  }
  dialogVisible.value = false
  fetchData()
}

onMounted(() => { fetchData() })
</script>

<template>
  <div class="page-container">
    <h2>字典类型管理</h2>

    <div class="toolbar">
      <div />
      <el-button type="danger" @click="handleCreate">+ 新建字典类型</el-button>
    </div>

    <el-table :data="tableData" v-loading="loading" stripe border style="width: 100%">
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column prop="dictTypeCode" label="字典类型编号" width="252" show-overflow-tooltip />
      <el-table-column prop="dictTypeName" label="字典类型名称" width="252" show-overflow-tooltip />
      <el-table-column prop="remark" label="备注" min-width="200" show-overflow-tooltip />
      <el-table-column prop="itemCount" label="项数量" width="80" align="center" />
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
      :title="editingId ? '编辑字典类型' : '新建字典类型'"
      width="480px"
      :close-on-click-modal="false"
    >
      <el-form ref="formRef" :model="form" :rules="formRules" label-width="120px">
        <el-form-item label="字典类型编号" prop="dictTypeCode">
          <el-input v-model="form.dictTypeCode" placeholder="如 project_type" maxlength="50" />
        </el-form-item>
        <el-form-item label="字典类型名称" prop="dictTypeName">
          <el-input v-model="form.dictTypeName" placeholder="如 项目类型" maxlength="50" />
        </el-form-item>
        <el-form-item label="备注" prop="remark">
          <el-input v-model="form.remark" placeholder="可选" maxlength="200" type="textarea" :rows="2" />
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
