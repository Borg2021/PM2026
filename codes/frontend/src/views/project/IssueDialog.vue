<script setup lang="ts">
import { reactive, ref, watch, computed } from 'vue'
import { ElMessage } from 'element-plus'
import { getDictByType, searchUsers, getDepartments } from '@/api/template'
import { getProjectList } from '@/api/project'
import type { CreateIssueRequest, IssueMeasure, ProjectIssue } from '@/types/issue'
import type { Department, UserInfo } from '@/types/template'
import type { Project } from '@/types/project'

const props = defineProps<{
  visible: boolean
  issue?: ProjectIssue | null
  projectId?: number
  showProjectSelect?: boolean
}>()

const emit = defineEmits<{
  (e: 'update:visible', v: boolean): void
  (e: 'saved'): void
}>()

const saving = ref(false)

const form = reactive<CreateIssueRequest & { projectId?: number }>({
  projectId: props.projectId,
  title: '',
  description: '',
  issueSource: '',
  issueType: '',
  severity: '一般',
  priority: '一般',
  causeAnalysis: '',
  discoveredDate: '',
  plannedDate: '',
  responsibleDeptId: undefined,
  responsibleDeptName: '',
  assigneeId: 0,
  assigneeName: '',
  submitterId: 0,
  submitterName: '',
  verifierId: undefined,
  verifierName: '',
  verifiedDate: '',
  measures: [{ sortOrder: 1, measure: '' }]
})

// 数据源
const issueSourceOptions = ref<{ dictCode: string; dictLabel: string }[]>([])
const issueTypeOptions = ref<{ dictCode: string; dictLabel: string }[]>([])
const measureTypeOptions = ref<{ dictCode: string; dictLabel: string }[]>([])
const departments = ref<Department[]>([])
const users = ref<UserInfo[]>([])
const projects = ref<Project[]>([])
const deptUsers = ref<UserInfo[]>([])

async function loadDicts() {
  try {
    const [sourceRes, typeRes, mTypeRes] = await Promise.all([
      getDictByType('issue_source'),
      getDictByType('issue_type'),
      getDictByType('measure_type')
    ])
    issueSourceOptions.value = (sourceRes.data || []) as any[]
    issueTypeOptions.value = (typeRes.data || []) as any[]
    measureTypeOptions.value = (mTypeRes.data || []) as any[]
  } catch { /* ignore */ }
}

async function loadDepartments() {
  try {
    const res = await getDepartments()
    departments.value = res.data || []
  } catch { /* ignore */ }
}

async function loadUsers() {
  try {
    const res = await searchUsers({})
    users.value = res.data || []
  } catch { /* ignore */ }
}

async function loadProjects() {
  try {
    const res = await getProjectList({ pageIndex: 1, pageSize: 999 })
    projects.value = (res.data?.items || []) as Project[]
  } catch { /* ignore */ }
}

// 重置表单
function resetForm() {
  form.projectId = props.projectId
  form.title = ''
  form.description = ''
  form.issueSource = ''
  form.issueType = ''
  form.severity = '一般'
  form.priority = '一般'
  form.causeAnalysis = ''
  form.discoveredDate = ''
  form.plannedDate = ''
  form.responsibleDeptId = undefined
  form.responsibleDeptName = ''
  form.assigneeId = 0
  form.assigneeName = ''
  form.submitterId = 0
  form.submitterName = ''
  form.verifierId = undefined
  form.verifierName = ''
  form.verifiedDate = ''
  form.measures = [{ sortOrder: 1, measure: '' }]
}

function loadIssueData() {
  if (props.issue) {
    form.title = props.issue.title
    form.description = props.issue.description
    form.issueSource = props.issue.issueSource
    form.issueType = props.issue.issueType
    form.severity = props.issue.severity
    form.priority = props.issue.priority
    form.causeAnalysis = props.issue.causeAnalysis
    form.discoveredDate = props.issue.discoveredDate?.slice(0, 10) || ''
    form.plannedDate = props.issue.plannedDate?.slice(0, 10) || ''
    form.responsibleDeptId = props.issue.responsibleDeptId
    form.responsibleDeptName = props.issue.responsibleDeptName
    form.assigneeId = props.issue.assigneeId
    form.assigneeName = props.issue.assigneeName
    form.submitterId = props.issue.submitterId
    form.submitterName = props.issue.submitterName
    form.verifierId = props.issue.verifierId
    form.verifierName = props.issue.verifierName
    form.verifiedDate = props.issue.verifiedDate?.slice(0, 10) || ''
    form.measures = (props.issue.measures && props.issue.measures.length > 0)
      ? props.issue.measures.map(m => ({ ...m }))
      : [{ sortOrder: 1, measure: '' }]
  }
}

watch(() => props.visible, (v) => {
  if (v) {
    resetForm()
    if (props.issue) loadIssueData()
    loadDicts()
    loadDepartments()
    loadUsers()
    if (props.showProjectSelect) loadProjects()
  }
})

// 部门→人员联动
function onDeptChange(deptId: number | undefined) {
  if (deptId) {
    const dept = departments.value.find(d => d.id === deptId)
    if (dept && dept.name) form.responsibleDeptName = dept.name
    deptUsers.value = users.value.filter(u => u.departmentId === deptId)
  } else {
    form.responsibleDeptName = ''
    deptUsers.value = []
  }
}

function onAssigneeChange(userId: number) {
  const user = users.value.find(u => u.id === userId)
  if (user) {
    form.assigneeName = user.realName || user.username
    if (!form.responsibleDeptId && user.departmentId) {
      form.responsibleDeptId = user.departmentId
      const dept = departments.value.find(d => d.id === user.departmentId)
      if (dept) form.responsibleDeptName = dept.name || ''
    }
  }
}

function onDeptAssigneeChange(userId: number | undefined, rowIndex: number) {
  if (!userId) return
  const user = users.value.find(u => u.id === userId)
  if (user) {
    form.measures[rowIndex].responsibleUserName = user.realName || user.username
    if (!form.measures[rowIndex].responsibleDeptId && user.departmentId) {
      form.measures[rowIndex].responsibleDeptId = user.departmentId
      const dept = departments.value.find(d => d.id === user.departmentId)
      if (dept) form.measures[rowIndex].responsibleDeptName = dept.name || ''
    }
  }
}

// 措施管理
function addMeasure() {
  form.measures.push({ sortOrder: form.measures.length + 1, measure: '' })
}

function removeMeasure(index: number) {
  if (form.measures.length <= 1) return
  form.measures.splice(index, 1)
  form.measures.forEach((m, i) => m.sortOrder = i + 1)
}

function validateForm(): boolean {
  if (!form.title.trim()) { ElMessage.warning('请输入问题标题'); return false }
  if (!form.discoveredDate) { ElMessage.warning('请选择发现日期'); return false }
  if (!form.issueSource) { ElMessage.warning('请选择问题来源'); return false }
  if (!form.issueType) { ElMessage.warning('请选择问题类型'); return false }
  if (!form.assigneeId) { ElMessage.warning('请选择责任人'); return false }
  if (!form.submitterId) { ElMessage.warning('请选择提出人'); return false }
  if (props.showProjectSelect && !form.projectId) { ElMessage.warning('请选择所属项目'); return false }
  return true
}

const emitSave = () => {
  if (!validateForm()) return
  emit('saved')
}

defineExpose({ form, saving })
</script>

<template>
  <el-dialog :model-value="visible" @update:model-value="$emit('update:visible', $event)"
    :title="issue ? '编辑问题' : '新建问题'" width="850px" destroy-on-close top="3vh">
    <div style="max-height:70vh;overflow-y:auto;padding-right:8px">
      <!-- 项目选择（全局模式） -->
      <el-form-item v-if="showProjectSelect" label="所属项目" label-width="90px" style="margin-bottom:12px">
        <el-select v-model="form.projectId" placeholder="请选择项目" filterable style="width:100%">
          <el-option v-for="p in projects" :key="p.id" :label="`${p.projectCode} - ${p.projectName}`" :value="p.id" />
        </el-select>
      </el-form-item>

      <!-- 区域一：基本信息 -->
      <el-card shadow="never" size="small" style="margin-bottom:12px">
        <template #header><span style="font-weight:600">基本信息</span></template>
        <el-form label-width="80px" label-position="top" size="small">
          <el-row :gutter="12">
            <el-col :span="24">
              <el-form-item label="问题标题" required><el-input v-model="form.title" placeholder="请输入问题标题" maxlength="200" /></el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="问题来源" required>
                <el-select v-model="form.issueSource" placeholder="请选择" style="width:100%">
                  <el-option v-for="o in issueSourceOptions" :key="o.dictCode" :label="o.dictLabel" :value="o.dictCode" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="问题类型" required>
                <el-select v-model="form.issueType" placeholder="请选择" style="width:100%">
                  <el-option v-for="o in issueTypeOptions" :key="o.dictCode" :label="o.dictLabel" :value="o.dictCode" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="发现日期" required><el-date-picker v-model="form.discoveredDate" type="date" placeholder="选择日期" style="width:100%" value-format="YYYY-MM-DD" /></el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="严重程度">
                <el-select v-model="form.severity" style="width:100%">
                  <el-option label="重要" value="重要" /><el-option label="一般" value="一般" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="优先级">
                <el-select v-model="form.priority" style="width:100%">
                  <el-option label="紧急" value="紧急" /><el-option label="一般" value="一般" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="计划完成"><el-date-picker v-model="form.plannedDate" type="date" placeholder="选择日期" style="width:100%" value-format="YYYY-MM-DD" /></el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="责任部门">
                <el-select v-model="form.responsibleDeptId" placeholder="请选择" style="width:100%" clearable @change="onDeptChange(form.responsibleDeptId)">
                  <el-option v-for="d in departments" :key="d.id" :label="d.name" :value="d.id" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="责任人" required>
                <el-select v-model="form.assigneeId" placeholder="请选择" style="width:100%" filterable @change="onAssigneeChange(form.assigneeId)">
                  <el-option v-for="u in (form.responsibleDeptId ? deptUsers : users)" :key="u.id" :label="u.realName || u.username" :value="u.id" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="提出人" required>
                <el-select v-model="form.submitterId" placeholder="请选择" style="width:100%" filterable>
                  <el-option v-for="u in users" :key="u.id" :label="u.realName || u.username" :value="u.id" />
                </el-select>
              </el-form-item>
            </el-col>
          </el-row>
          <el-row :gutter="12">
            <el-col :span="8">
              <el-form-item label="验证人">
                <el-select v-model="form.verifierId" placeholder="请选择" style="width:100%" clearable filterable>
                  <el-option v-for="u in users" :key="u.id" :label="u.realName || u.username" :value="u.id" />
                </el-select>
              </el-form-item>
            </el-col>
            <el-col :span="8">
              <el-form-item label="验证日期"><el-date-picker v-model="form.verifiedDate" type="date" placeholder="选择日期" style="width:100%" value-format="YYYY-MM-DD" /></el-form-item>
            </el-col>
          </el-row>
          <el-form-item label="问题描述"><el-input v-model="form.description" type="textarea" :rows="2" placeholder="问题详细描述（可选）" maxlength="2000" /></el-form-item>
          <el-form-item label="原因分析"><el-input v-model="form.causeAnalysis" type="textarea" :rows="2" placeholder="根本原因分析（可选）" maxlength="2000" /></el-form-item>
        </el-form>
      </el-card>

      <!-- 区域二：措施清单 -->
      <el-card shadow="never" size="small" style="margin-bottom:12px">
        <template #header>
          <div style="display:flex;justify-content:space-between;align-items:center">
            <span style="font-weight:600">措施清单</span>
            <el-button size="small" type="primary" text @click="addMeasure">+ 添加措施</el-button>
          </div>
        </template>
        <div v-for="(m, idx) in form.measures" :key="idx" style="border:1px solid #ebeef5;border-radius:4px;padding:8px;margin-bottom:8px">
          <el-row :gutter="8" align="middle">
            <el-col :span="1"><span style="color:#909399">{{ idx + 1 }}</span></el-col>
            <el-col :span="5">
              <el-input v-model="m.measure" placeholder="措施内容" size="small" />
            </el-col>
            <el-col :span="3">
              <el-select v-model="m.measureType" placeholder="类型" size="small" style="width:100%" clearable>
                <el-option v-for="o in measureTypeOptions" :key="o.dictCode" :label="o.dictLabel" :value="o.dictCode" />
              </el-select>
            </el-col>
            <el-col :span="3">
              <el-select v-model="m.responsibleDeptId" placeholder="部门" size="small" style="width:100%" clearable @change="(v: number) => { if(v){ const d = departments.find(dd => dd.id === v); if(d) { m.responsibleDeptName = d.name } } else m.responsibleDeptName = '' }">
                <el-option v-for="d in departments" :key="d.id" :label="d.name" :value="d.id" />
              </el-select>
            </el-col>
            <el-col :span="3">
              <el-select v-model="m.responsibleUserId" placeholder="责任人" size="small" style="width:100%" clearable filterable @change="(v: number) => onDeptAssigneeChange(v, idx)">
                <el-option v-for="u in users" :key="u.id" :label="u.realName || u.username" :value="u.id" />
              </el-select>
            </el-col>
            <el-col :span="3"><el-input v-model="m.remark" placeholder="备注" size="small" /></el-col>
            <el-col :span="3"><el-date-picker v-model="m.plannedDate" type="date" placeholder="计划完成" size="small" style="width:100%" value-format="YYYY-MM-DD" /></el-col>
            <el-col :span="2" style="text-align:center">
              <el-button size="small" type="danger" text @click="removeMeasure(idx)" :disabled="form.measures.length <= 1">删除</el-button>
            </el-col>
          </el-row>
        </div>
      </el-card>
    </div>

    <template #footer>
      <el-button @click="$emit('update:visible', false)">取消</el-button>
      <el-button type="primary" @click="emitSave" :loading="saving">保存</el-button>
    </template>
  </el-dialog>
</template>
