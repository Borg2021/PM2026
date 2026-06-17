<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { createTemplate, getTemplateDetail, updateTemplate, saveMilestones } from '@/api/template'
import type { Milestone } from '@/types/template'
import MilestoneTable from './MilestoneTable.vue'

const route = useRoute()
const router = useRouter()

/** Determine editor mode from route name */
const mode = computed<'create' | 'edit' | 'view'>(() => {
  const name = route.name as string
  if (name === 'MilestoneCreate') return 'create'
  if (name === 'MilestoneEdit') return 'edit'
  return 'view'
})

const isView = computed(() => mode.value === 'view')

const pageTitle = computed(() => {
  const map: Record<string, string> = {
    create: '新建里程碑模板',
    edit: '编辑里程碑模板',
    view: '查看里程碑模板'
  }
  return map[mode.value]
})

// ── Form state ────────────────────────────────────────────
const form = ref({
  id: 0,
  templateCode: '',
  templateName: '',
  description: ''
})

const milestones = ref<Milestone[]>([])
const pageLoading = ref(false)
const saveLoading = ref(false)

// ── Data fetching ─────────────────────────────────────────
async function fetchDetail(id: number): Promise<void> {
  pageLoading.value = true
  try {
    const res = await getTemplateDetail(id)
    const data = res.data
    form.value = {
      id: data.id,
      templateCode: data.templateCode,
      templateName: data.templateName,
      description: data.description
    }
    milestones.value = data.milestones?.length ? data.milestones : []
  } catch {
    ElMessage.error('获取模板详情失败')
  } finally {
    pageLoading.value = false
  }
}

onMounted(() => {
  const id = route.params.id ? Number(route.params.id) : null
  if (id) {
    fetchDetail(id)
  }
})

// ── Save handler ──────────────────────────────────────────
async function handleSave(): Promise<void> {
  if (!form.value.templateName.trim()) {
    ElMessage.warning('请输入模板名称')
    return
  }

  saveLoading.value = true
  try {
    let templateId = form.value.id

    if (mode.value === 'create') {
      const res = await createTemplate({
        templateCode: `M${Date.now()}`,
        templateName: form.value.templateName,
        templateType: 2,
        description: form.value.description
      })
      templateId = res.data.id
    } else {
      await updateTemplate(templateId, {
        templateName: form.value.templateName,
        description: form.value.description
      })
    }

    // Assign sort order before saving
    const sortedMilestones = milestones.value.map((m, idx) => ({
      ...m,
      sortOrder: idx + 1
    }))
    await saveMilestones(templateId, sortedMilestones)

    ElMessage.success('保存成功')
    router.push('/template/list')
  } catch {
    // HTTP errors are handled by the request interceptor
  } finally {
    saveLoading.value = false
  }
}

function handleCancel(): void {
  router.push('/template/list')
}

function handleReturn(): void {
  router.push('/template/list')
}
</script>

<template>
  <div class="milestone-editor">
    <!-- Page title -->
    <div class="page-header">
      <h3>{{ pageTitle }}</h3>
    </div>

    <el-skeleton :loading="pageLoading" animated>
      <template #default>
        <!-- ── Basic Info ────────────────────────────── -->
        <el-card class="section-card" shadow="never">
          <template #header>
            <span>基本信息</span>
          </template>
          <el-form label-width="90px" label-position="right">
            <el-row :gutter="24">
              <el-col :span="8">
                <el-form-item label="模板编号">
                  <el-input
                    :model-value="mode === 'create' ? '自动生成' : form.templateCode"
                    disabled
                  />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="模板名称">
                  <el-input
                    v-model="form.templateName"
                    :readonly="isView"
                    :disabled="isView"
                    placeholder="请输入模板名称"
                  />
                </el-form-item>
              </el-col>
            </el-row>
            <el-row>
              <el-col :span="16">
                <el-form-item label="模板描述">
                  <el-input
                    v-model="form.description"
                    type="textarea"
                    :rows="3"
                    :readonly="isView"
                    :disabled="isView"
                    placeholder="请输入模板描述"
                  />
                </el-form-item>
              </el-col>
            </el-row>
          </el-form>
        </el-card>

        <!-- ── Milestone Table ───────────────────────── -->
        <el-card class="section-card" shadow="never">
          <template #header>
            <span>里程碑配置</span>
          </template>
          <MilestoneTable v-model="milestones" :readonly="isView" />
        </el-card>

        <!-- ── Form Actions ──────────────────────────── -->
        <div class="form-actions">
          <template v-if="isView">
            <el-button plain @click="handleReturn">返 回</el-button>
          </template>
          <template v-else>
            <el-button type="danger" :loading="saveLoading" @click="handleSave">保 存</el-button>
            <el-button plain @click="handleCancel">取 消</el-button>
          </template>
        </div>
      </template>
    </el-skeleton>
  </div>
</template>

<style scoped>
.milestone-editor {
  padding: 20px;
  max-width: 960px;
  margin: 0 auto;
}

.page-header {
  margin-bottom: 16px;
}

.page-header h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #303133;
}

.section-card {
  margin-bottom: 20px;
}

.form-actions {
  text-align: center;
  padding: 24px 0 0;
}

.form-actions .el-button {
  min-width: 100px;
}

.form-actions .el-button + .el-button {
  margin-left: 12px;
}
</style>
