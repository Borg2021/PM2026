<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import type { PlanNode } from '@/types/template'
import { getTemplateDetail, createTemplate, updateTemplate, savePlanNodes } from '@/api/template'
import PlanNodeTree from './PlanNodeTree.vue'
import AddPlanNodeDialog from './AddPlanNodeDialog.vue'

const route = useRoute()
const router = useRouter()

/* ---- 模式判定 ---- */
const templateId = computed(() => {
  const raw = route.params.id
  return raw ? Number(raw) : null
})

const routeName = computed(() => route.name as string | undefined)
const isCreateMode = computed(() => routeName.value === 'PlanCreate')
const isEditMode = computed(() => routeName.value === 'PlanEdit')
const isViewMode = computed(() => routeName.value === 'PlanView')
const isFormEditable = computed(() => isEditMode.value || isCreateMode.value)

/* ---- 基础信息表单 ---- */
const formRef = ref()
const templateCode = ref('')
const templateName = ref('')
const description = ref('')

const formRules = {
  templateName: [
    { required: true, message: '请输入模板名称', trigger: 'blur' },
    { max: 100, message: '模板名称不超过 100 个字符', trigger: 'blur' }
  ]
}

/* ---- 节点树 ---- */
const planNodes = ref<PlanNode[]>([])
const treeRef = ref<InstanceType<typeof PlanNodeTree>>()

/* ---- 新增/编辑节点弹窗 ---- */
const dialogVisible = ref(false)
const dialogMode = ref<'add' | 'edit'>('add')
const dialogNodeData = ref<PlanNode | null>(null)   // 编辑时传入现有节点
const editingNodeKey = ref<string | null>(null)       // 编辑时记住内部 key
const editingParentKey = ref<string | null>(null)     // 编辑时记住父节点 key
const addingParentKey = ref<string | null>(null)      // 新增时记住父节点 _nodeKey
const addingParentCode = ref<string | null>(null)     // 新增时父节点 nodeCode（用于预览编号）

/** 打开新增弹窗（parentKey = null 表示添加根节点） */
function handleAddChild(parentKey: string | null) {
  dialogMode.value = 'add'
  // _nodeKey 用于 addNode，nodeCode 用于对话框预览编号
  addingParentKey.value = parentKey
  addingParentCode.value = parentKey
    ? (treeRef.value?.getNodeCodeByKey(parentKey) ?? null)
    : null
  editingNodeKey.value = null
  dialogNodeData.value = null
  dialogVisible.value = true
}

/** 打开编辑弹窗 */
function handleEditNode(node: PlanNode) {
  dialogMode.value = 'edit'
  const nodeKey = (node as Record<string, unknown>)._nodeKey as string ?? null
  editingNodeKey.value = nodeKey
  editingParentKey.value = nodeKey ? (treeRef.value?.getParentNodeCode(nodeKey) ?? null) : null
  addingParentKey.value = null
  dialogNodeData.value = { ...node }
  dialogVisible.value = true
}

/** 弹窗确认回调 */
function handleDialogConfirm(node: PlanNode, targetParentCode: string | null) {
  if (dialogMode.value === 'edit' && editingNodeKey.value) {
    const newParent = targetParentCode || null
    treeRef.value?.updateNodeAndMove(editingNodeKey.value, node, newParent)
  } else {
    treeRef.value?.addNode(addingParentKey.value, node)
  }
  dialogVisible.value = false
}

/* ---- 保存 ---- */
const saveLoading = ref(false)

async function handleSave() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  saveLoading.value = true
  try {
    let id = templateId.value

    if (isCreateMode.value) {
      // 先创建模板，拿到 id 后再保存节点
      const res = await createTemplate({
        templateCode: templateCode.value,
        templateName: templateName.value,
        templateType: 1,
        description: description.value || undefined
      })
      id = res.data.id
    } else if (id) {
      await updateTemplate(id, {
        templateName: templateName.value,
        description: description.value || undefined
      })
    }

    if (id) {
      // 从 tree 组件获取最新节点数据（已剥离内部 key）
      const nodes = treeRef.value?.getCleanedNodes() ?? []
      await savePlanNodes(id, nodes)
    }

    ElMessage.success('保存成功')
    router.push('/template/list')
  } catch {
    ElMessage.error('保存失败，请重试')
  } finally {
    saveLoading.value = false
  }
}

function handleCancel() {
  router.push('/template/list')
}

/* ---- 初始化 ---- */
onMounted(async () => {
  if (templateId.value) {
    try {
      const res = await getTemplateDetail(templateId.value)

      const detail = res.data
      templateCode.value = detail.templateCode
      templateName.value = detail.templateName
      description.value = detail.description

      planNodes.value = detail.planNodes ?? []
    } catch {
      ElMessage.error('加载模板详情失败')
    }
  } else {
    templateCode.value = (route.query.templateCode as string) || ''
    templateName.value = (route.query.templateName as string) || ''
    description.value = (route.query.description as string) || ''
  }
})
</script>

<template>
  <div class="plan-template-editor">
    <!-- 页面标题 -->
    <h2 class="page-title">
      {{ isCreateMode ? '新建计划模板' : isViewMode ? '查看计划模板' : '编辑计划模板' }}
    </h2>

    <!-- 基础信息 -->
    <el-form
      ref="formRef"
      :model="{ templateName, description }"
      :rules="formRules"
      label-width="100px"
      label-position="right"
      class="basic-form"
      :disabled="isViewMode"
    >
      <el-form-item label="模板编号">
        <el-input
          v-model="templateCode"
          placeholder="自动生成或由上一页传入"
          disabled
          maxlength="50"
        />
      </el-form-item>

      <el-form-item label="模板名称" prop="templateName">
        <el-input
          v-model="templateName"
          placeholder="请输入模板名称"
          :readonly="isViewMode"
          maxlength="100"
        />
      </el-form-item>

      <el-form-item label="模板描述" prop="description">
        <el-input
          v-model="description"
          type="textarea"
          :rows="3"
          placeholder="请输入模板描述（选填）"
          :readonly="isViewMode"
          maxlength="500"
          show-word-limit
        />
      </el-form-item>
    </el-form>

    <!-- 计划节点树 -->
    <PlanNodeTree
      ref="treeRef"
      v-model="planNodes"
      :readonly="isViewMode"
      @add-child="handleAddChild"
      @edit-node="handleEditNode"
    />

    <!-- 操作按钮 -->
    <div class="form-actions">
      <el-button type="danger" :loading="saveLoading" @click="handleSave" v-if="isFormEditable">
        保存
      </el-button>
      <el-button @click="handleCancel">取消</el-button>
    </div>

    <!-- 新增 / 编辑节点弹窗 -->
    <AddPlanNodeDialog
      :visible="dialogVisible"
      :node-data="dialogNodeData"
      :all-nodes="planNodes"
      :current-parent-node-code="dialogMode === 'edit' ? editingParentKey : addingParentCode"
      @update:visible="dialogVisible = $event"
      @confirm="handleDialogConfirm"
    />
  </div>
</template>

<style scoped>
.plan-template-editor {
  padding: 24px;
}

.page-title {
  margin: 0 0 20px;
  font-size: 20px;
  font-weight: 600;
  color: #303133;
}

.basic-form {
  max-width: 720px;
  margin-bottom: 24px;
}

.form-actions {
  margin-top: 24px;
  display: flex;
  gap: 12px;
}
</style>
