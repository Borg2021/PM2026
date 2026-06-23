<script setup lang="ts">
import { computed } from 'vue'
import type { ProjectTaskItem } from '@/types/project'
import type { Department, UserInfo } from '@/types/template'
import { formatPreTaskCodes } from '@/utils/preTaskHelpers'
import { taskStatusOptions, taskPriorityOptions } from '@/utils/taskConstants'
import { buildDeptTree } from '@/utils/deptTree'

const props = defineProps<{
  visible: boolean
  task: ProjectTaskItem | null
  dictMap: Record<string, { id: number; dictCode: string; dictLabel: string }[]>
  users: UserInfo[]
  departments: Department[]
  allTasks: ProjectTaskItem[]
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
}>()

const taskIdMap = computed(() => {
  const map = new Map<number, ProjectTaskItem>()
  for (const t of props.allTasks) {
    if (t.id) map.set(t.id, t)
  }
  return map
})

const deptTreeData = computed(() => buildDeptTree(props.departments))
</script>

<template>
  <el-dialog :model-value="visible" title="任务详情" width="840px" :close-on-click-modal="false" top="5vh" @update:model-value="emit('update:visible', $event)">
    <el-form v-if="task" :model="task" label-width="100px" size="default" disabled>
      <el-row :gutter="16">
        <el-col :span="12">
          <el-form-item label="序号">
            <el-input :model-value="task.taskNo" disabled />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="任务名称">
            <el-input :model-value="task.taskName" />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="工序号">
            <el-input :model-value="task.wbsCode || ''" />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="任务类别">
            <el-select :model-value="task.taskCategory" placeholder="请选择" style="width:100%">
              <el-option v-for="item in (dictMap['task_category'] || [])" :key="item.dictCode" :label="item.dictLabel" :value="item.dictCode" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="节点类型">
            <el-radio-group :model-value="task.nodeType">
              <el-radio :value="1">任务</el-radio>
              <el-radio :value="2">里程碑</el-radio>
            </el-radio-group>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="优先级">
            <el-select :model-value="task.priority" style="width:100%">
              <el-option v-for="o in taskPriorityOptions" :key="o.value" :label="o.label" :value="o.value" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="状态">
            <el-select :model-value="task.status" style="width:100%">
              <el-option v-for="o in taskStatusOptions" :key="o.value" :label="o.label" :value="o.value" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="责任部门">
            <el-tree-select
              :model-value="task.deptId"
              :data="deptTreeData"
              :props="{ label: 'name', children: 'children', value: 'id' }"
              node-key="id"
              check-strictly
              placeholder="请选择部门"
              clearable
              filterable
              style="width:100%"
            />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="责任人">
            <el-select :model-value="task.assigneeName" filterable clearable placeholder="请选择人员" style="width:100%">
              <el-option v-for="u in users.filter(u => !task.deptId || u.departmentId === task.deptId)" :key="u.id" :label="u.realName" :value="u.realName" />
            </el-select>
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="参考工期">
            <el-input-number :model-value="task.referenceDuration" :min="0" style="width:100%" />
          </el-form-item>
        </el-col>
        <el-col :span="24">
          <el-form-item label="前置任务">
            <el-input :model-value="formatPreTaskCodes(task.preTaskCodes, taskIdMap) || '尚未设置前置任务'" disabled />
          </el-form-item>
        </el-col>
        <el-col :span="12">
          <el-form-item label="完成进度(%)">
            <el-input-number :model-value="task.progressPct" :min="0" :max="100" :precision="1" style="width:100%" />
          </el-form-item>
        </el-col>
        <el-col :span="24">
          <el-card shadow="never" class="group-card">
            <template #header><span class="group-title">计划时间</span></template>
            <el-row :gutter="24">
              <el-col :span="8">
                <el-form-item label="计划开始">
                  <el-date-picker :model-value="task.planStartDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="计划完成">
                  <el-date-picker :model-value="task.planFinishDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="计划工期">
                  <el-input-number :model-value="task.planDuration" :min="0" style="width:100%" disabled />
                </el-form-item>
              </el-col>
            </el-row>
          </el-card>
        </el-col>
        <el-col :span="24">
          <el-card shadow="never" class="group-card">
            <template #header><span class="group-title">实际时间</span></template>
            <el-row :gutter="24">
              <el-col :span="8">
                <el-form-item label="实际开始">
                  <el-date-picker :model-value="task.actualStartDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="实际完成">
                  <el-date-picker :model-value="task.actualFinishDate" type="date" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
                </el-form-item>
              </el-col>
              <el-col :span="8">
                <el-form-item label="实际工期">
                  <el-input-number :model-value="task.actualDuration" :min="0" style="width:100%" disabled />
                </el-form-item>
              </el-col>
            </el-row>
          </el-card>
        </el-col>
        <el-col :span="24">
          <el-form-item label="备注" class="remark-item">
            <el-input :model-value="task.remark || ''" type="textarea" :rows="2" />
          </el-form-item>
        </el-col>
      </el-row>
    </el-form>
    <template #footer>
      <el-button type="primary" @click="emit('update:visible', false)">关闭</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.group-card {
  margin-bottom: 12px;
  border: 1px solid #dcdfe6;
}
.group-card :deep(.el-card__header) {
  padding: 8px 16px;
  background: #f5f7fa;
  border-bottom: 1px solid #dcdfe6;
}
.group-title {
  font-size: 13px;
  font-weight: 600;
  color: #606266;
}
.remark-item {
  margin-top: 12px;
}
</style>
