<script setup lang="ts">
import { ref, reactive, onMounted, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { getTemplateList, createPlanBundle, updatePlanBundle, getPlanBundleDetail } from '@/api/template'
import type { Template, PlanBundleItem } from '@/types/template'

const router = useRouter()
const route = useRoute()
const isEdit = computed(() => !!route.params.id)

const form = reactive({ name: '', description: '' })
const formRules = { name: [{ required: true, message: '请输入模板集名称', trigger: 'blur' }] }
const formRef = ref()

const allTemplates = ref<Template[]>([])
const selectedItems = ref<{ templateId: number; templateCode: string; templateName: string; sortOrder: number }[]>([])
const templateSearch = ref('')

const filteredTemplates = computed(() => {
  if (!templateSearch.value) return allTemplates.value
  const kw = templateSearch.value.toLowerCase()
  return allTemplates.value.filter(t =>
    t.templateName.toLowerCase().includes(kw) || t.templateCode.toLowerCase().includes(kw)
  )
})

const selectedIds = computed(() => new Set(selectedItems.value.map(i => i.templateId)))

function addTemplate(tpl: Template) {
  if (selectedIds.value.has(tpl.id)) return
  selectedItems.value.push({
    templateId: tpl.id,
    templateCode: tpl.templateCode,
    templateName: tpl.templateName,
    sortOrder: selectedItems.value.length + 1
  })
}

function removeItem(index: number) {
  selectedItems.value.splice(index, 1)
  selectedItems.value.forEach((item, i) => { item.sortOrder = i + 1 })
}

function moveItem(index: number, dir: 'up' | 'down') {
  const arr = selectedItems.value
  const target = dir === 'up' ? index - 1 : index + 1
  if (target < 0 || target >= arr.length) return;
  [arr[index], arr[target]] = [arr[target], arr[index]];
  arr.forEach((item, i) => { item.sortOrder = i + 1 })
}

async function submit() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return
  if (selectedItems.value.length === 0) {
    ElMessage.warning('请至少选择一个计划模板')
    return
  }
  const data = {
    name: form.name.trim(),
    description: form.description.trim() || undefined,
    items: selectedItems.value.map(i => ({ templateId: i.templateId, sortOrder: i.sortOrder }))
  }
  if (isEdit.value) {
    await updatePlanBundle(Number(route.params.id), data)
    ElMessage.success('更新成功')
  } else {
    await createPlanBundle(data)
    ElMessage.success('创建成功')
  }
  router.push('/template/bundles')
}


onMounted(async () => {
  // 加载所有计划类型模板
  const res = await getTemplateList({ pageIndex: 1, pageSize: 200, templateType: 1 })
  allTemplates.value = res.data.items

  if (isEdit.value) {
    const detail = await getPlanBundleDetail(Number(route.params.id))
    form.name = detail.data.name
    form.description = detail.data.description || ''
    selectedItems.value = detail.data.items.map(i => ({
      templateId: i.templateId,
      templateCode: i.templateCode,
      templateName: i.templateName,
      sortOrder: i.sortOrder
    }))
  }
})
</script>

<template>
  <div class="page-container">
    <h2>{{ isEdit ? '编辑模板集' : '新建模板集' }}</h2>

    <el-card shadow="never">
      <el-form ref="formRef" :model="form" :rules="formRules" label-width="80px" style="max-width: 600px">
        <el-form-item label="名称" prop="name">
          <el-input v-model="form.name" placeholder="模板集名称" maxlength="50" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="form.description" type="textarea" :rows="3" placeholder="描述（选填）" maxlength="500" show-word-limit />
        </el-form-item>
      </el-form>
    </el-card>

    <el-card shadow="never" style="margin-top: 16px">
      <template #header>选择计划模板</template>
      <el-row :gutter="16">
        <el-col :span="10">
          <div class="panel-title">可选模板</div>
          <el-input v-model="templateSearch" placeholder="搜索模板..." clearable style="margin-bottom: 8px" />
          <div class="template-list">
            <div
              v-for="tpl in filteredTemplates"
              :key="tpl.id"
              class="template-item"
              :class="{ added: selectedIds.has(tpl.id) }"
              @click="addTemplate(tpl)"
            >
              <span class="tpl-code">{{ tpl.templateCode }}</span>
              <span class="tpl-name">{{ tpl.templateName }}</span>
              <el-icon v-if="selectedIds.has(tpl.id)" style="color: #67c23a"><Check /></el-icon>
            </div>
            <el-empty v-if="filteredTemplates.length === 0" description="无可用模板" :image-size="60" />
          </div>
        </el-col>
        <el-col :span="14">
          <div class="panel-title">已选模板（按组装顺序）</div>
          <div class="selected-list">
            <div v-for="(item, index) in selectedItems" :key="item.templateId" class="selected-item">
              <span class="order-badge">{{ item.sortOrder }}</span>
              <span class="tpl-code">{{ item.templateCode }}</span>
              <span class="tpl-name">{{ item.templateName }}</span>
              <div class="item-actions">
                <el-button link :disabled="index === 0" @click="moveItem(index, 'up')">
                  <el-icon><ArrowUp /></el-icon>
                </el-button>
                <el-button link :disabled="index === selectedItems.length - 1" @click="moveItem(index, 'down')">
                  <el-icon><ArrowDown /></el-icon>
                </el-button>
                <el-button link style="color: #f56c6c" @click="removeItem(index)">
                  <el-icon><Delete /></el-icon>
                </el-button>
              </div>
            </div>
            <el-empty v-if="selectedItems.length === 0" description="点击左侧模板添加" :image-size="60" />
          </div>
        </el-col>
      </el-row>
    </el-card>

    <div style="margin-top: 20px; text-align: right">
      <el-button @click="router.back()">取消</el-button>
      <el-button type="danger" @click="submit">保存</el-button>
    </div>
  </div>
</template>

<style scoped>
.page-container { padding: 24px; }
.page-container h2 { margin: 0 0 20px; font-size: 20px; font-weight: 600; color: #303133; }
.panel-title { font-weight: 600; margin-bottom: 10px; color: #303133; }
.template-list, .selected-list {
  border: 1px solid #e4e7ed; border-radius: 4px; min-height: 300px; max-height: 400px;
  overflow-y: auto; padding: 4px;
}
.template-item, .selected-item {
  display: flex; align-items: center; padding: 8px 10px; cursor: pointer;
  border-radius: 4px; gap: 8px;
}
.template-item:hover { background: #f5f7fa; }
.template-item.added { opacity: 0.5; cursor: not-allowed; }
.selected-item { background: #f5f7fa; margin-bottom: 4px; }
.tpl-code { color: #909399; font-size: 13px; min-width: 80px; }
.tpl-name { flex: 1; font-size: 14px; }
.order-badge {
  display: inline-flex; align-items: center; justify-content: center;
  width: 24px; height: 24px; background: #f56c6c; color: #fff;
  border-radius: 50%; font-size: 12px; font-weight: 600;
}
.item-actions { display: flex; gap: 2px; }
</style>
