<script setup lang="ts">
import { ref, shallowRef, reactive, onMounted } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getDictList, createDict, updateDict, deleteDict, getDictTypeList } from '@/api/system'
import type { DictItem, DictTypeItem } from '@/types/system'

const tableData = shallowRef<DictItem[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const editingId = ref<number | null>(null)
const dictTypeOptions = ref<DictTypeItem[]>([])
const filterType = ref('')

const form = reactive({
  dictType: '',
  dictCode: '',
  dictLabel: '',
  sortOrder: 0
})

const formRules = {
  dictType: [{ required: true, message: '请输入字典类型', trigger: 'blur' }],
  dictCode: [{ required: true, message: '请输入字典编码', trigger: 'blur' }],
  dictLabel: [{ required: true, message: '请输入字典标签', trigger: 'blur' }]
}

const formRef = ref()

function fetchData() {
  loading.value = true
  getDictList({ dictType: filterType.value || undefined })
    .then(res => { tableData.value = res.data })
    .finally(() => { loading.value = false })
}

function handleCreate() {
  editingId.value = null
  form.dictType = filterType.value || ''
  form.dictCode = ''
  form.dictLabel = ''
  form.sortOrder = 0
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

function handleEdit(row: DictItem) {
  editingId.value = row.id
  form.dictType = row.dictType
  form.dictCode = row.dictCode
  form.dictLabel = row.dictLabel
  form.sortOrder = row.sortOrder
  formRef.value?.clearValidate()
  dialogVisible.value = true
}

async function handleDelete(row: DictItem) {
  try {
    await ElMessageBox.confirm(`确定要删除字典项「${row.dictLabel}」吗？`, '确认删除', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
    await deleteDict(row.id)
    ElMessage.success('删除成功')
    fetchData()
  } catch { /* 取消或错误 */ }
}

function handleFilter() {
  fetchData()
}

function handleResetFilter() {
  filterType.value = ''
  fetchData()
}

async function submit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  if (editingId.value) {
    await updateDict(editingId.value, {
      dictType: form.dictType.trim(),
      dictCode: form.dictCode.trim(),
      dictLabel: form.dictLabel.trim(),
      sortOrder: form.sortOrder
    })
    ElMessage.success('更新成功')
  } else {
    await createDict({
      dictType: form.dictType.trim(),
      dictCode: form.dictCode.trim(),
      dictLabel: form.dictLabel.trim(),
      sortOrder: form.sortOrder
    })
    ElMessage.success('创建成功')
  }
  dialogVisible.value = false
  fetchData()
}

async function loadDictTypes() {
  try {
    const res = await getDictTypeList()
    dictTypeOptions.value = res.data
  } catch { /* 忽略 */ }
}

onMounted(() => {
  fetchData()
  loadDictTypes()
})
</script>

<template>
  <div class="page-container">
    <h2>字典管理</h2>

    <el-card shadow="never" class="filter-card">
      <el-row :gutter="16">
        <el-col :span="6">
          <el-select v-model="filterType" placeholder="按字典类型筛选" clearable style="width:100%" @change="handleFilter">
            <el-option v-for="t in dictTypeOptions" :key="t.dictTypeCode" :label="t.dictTypeName" :value="t.dictTypeCode" />
          </el-select>
        </el-col>
        <el-col :span="18">
          <el-button type="danger" @click="handleFilter">查询</el-button>
          <el-button @click="handleResetFilter">重置</el-button>
        </el-col>
      </el-row>
    </el-card>

    <div class="toolbar">
      <div />
      <el-button type="danger" @click="handleCreate">+ 新建字典项</el-button>
    </div>

    <el-table :data="tableData" v-loading="loading" stripe border style="width: 100%">
      <el-table-column prop="id" label="ID" width="70" />
      <el-table-column prop="dictType" label="字典类型" width="224" show-overflow-tooltip />
      <el-table-column prop="dictCode" label="字典编码" width="196" show-overflow-tooltip />
      <el-table-column prop="dictLabel" label="字典标签" min-width="160" show-overflow-tooltip />
      <el-table-column prop="sortOrder" label="排序" width="70" />
      <el-table-column label="状态" width="80">
        <template #default="{ row }">
          <el-tag :type="row.status === 1 ? 'success' : 'info'" size="small">
            {{ row.status === 1 ? '启用' : '禁用' }}
          </el-tag>
        </template>
      </el-table-column>
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
      :title="editingId ? '编辑字典项' : '新建字典项'"
      width="480px"
      :close-on-click-modal="false"
    >
      <el-form ref="formRef" :model="form" :rules="formRules" label-width="80px">
        <el-form-item label="字典类型" prop="dictType">
          <el-select v-model="form.dictType" placeholder="请选择字典类型" clearable style="width:100%">
            <el-option v-for="t in dictTypeOptions" :key="t.dictTypeCode" :label="t.dictTypeName" :value="t.dictTypeCode" />
          </el-select>
        </el-form-item>
        <el-form-item label="字典编码" prop="dictCode">
          <el-input v-model="form.dictCode" placeholder="编码值，如 internal" maxlength="50" />
        </el-form-item>
        <el-form-item label="字典标签" prop="dictLabel">
          <el-input v-model="form.dictLabel" placeholder="显示名称，如 内部项目" maxlength="50" />
        </el-form-item>
        <el-form-item label="排序" prop="sortOrder">
          <el-input-number v-model="form.sortOrder" :min="0" :max="999" />
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
.filter-card {
  margin-bottom: 16px;
}
.toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 16px;
}
</style>
