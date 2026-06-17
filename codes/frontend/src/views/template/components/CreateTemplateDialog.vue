<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { createTemplate } from '@/api/template'

const router = useRouter()

const props = defineProps<{
  visible: boolean
}>()

const emit = defineEmits<{
  (e: 'update:visible', val: boolean): void
  (e: 'created'): void
}>()

const formRef = ref()
const loading = ref(false)

const form = reactive({
  templateType: undefined as number | undefined,
  templateCode: '',
  templateName: '',
  description: ''
})

const rules = {
  templateType: [
    { required: true, message: '请选择模板种类', trigger: 'change' }
  ],
  templateCode: [
    { required: true, message: '请输入模板编号', trigger: 'blur' }
    // 若需要后端校验唯一性，可在此添加自定义 validator
  ],
  templateName: [
    { required: true, message: '请输入模板名称', trigger: 'blur' }
  ]
}

const templateTypeOptions = [
  { value: 1, label: '计划模板' },
  { value: 3, label: '项目成员' },
  { value: 4, label: '文件模板' }
]

const typeRouteMap: Record<number, string> = {
  1: 'plan',
  3: 'member',
  4: 'file'
}

function handleClose() {
  emit('update:visible', false)
}

function handleCancel() {
  emit('update:visible', false)
}

async function handleConfirm() {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid) return

  loading.value = true
  try {
    const res = await createTemplate({
      templateCode: form.templateCode,
      templateName: form.templateName,
      templateType: form.templateType!,
      description: form.description || undefined
    })

    const id = res.data.id
    const segment = typeRouteMap[form.templateType!]

    ElMessage.success('模板创建成功')
    emit('created')
    emit('update:visible', false)

    // 创建成功后跳转到对应的编辑页面
    router.push(`/template/${segment}/edit/${id}`)
  } catch {
    // 错误已由请求拦截器统一处理
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <el-dialog
    :model-value="props.visible"
    @update:model-value="emit('update:visible', $event)"
    title="新建模板"
    width="480px"
    :close-on-click-modal="false"
    @close="handleClose"
  >
    <el-form
      ref="formRef"
      :model="form"
      :rules="rules"
      label-width="90px"
      label-position="right"
    >
      <el-form-item label="模板种类" prop="templateType">
        <el-select
          v-model="form.templateType"
          placeholder="请选择模板种类"
          style="width: 100%"
        >
          <el-option
            v-for="item in templateTypeOptions"
            :key="item.value"
            :label="item.label"
            :value="item.value"
          />
        </el-select>
      </el-form-item>

      <el-form-item label="模板编号" prop="templateCode">
        <el-input
          v-model="form.templateCode"
          placeholder="请输入模板编号"
          maxlength="50"
        />
      </el-form-item>

      <el-form-item label="模板名称" prop="templateName">
        <el-input
          v-model="form.templateName"
          placeholder="请输入模板名称"
          maxlength="100"
        />
      </el-form-item>

      <el-form-item label="描述" prop="description">
        <el-input
          v-model="form.description"
          type="textarea"
          :rows="3"
          placeholder="请输入模板描述（选填）"
          maxlength="500"
          show-word-limit
        />
      </el-form-item>
    </el-form>

    <template #footer>
      <el-button @click="handleCancel">取消</el-button>
      <el-button type="danger" :loading="loading" @click="handleConfirm">确认</el-button>
    </template>
  </el-dialog>
</template>
