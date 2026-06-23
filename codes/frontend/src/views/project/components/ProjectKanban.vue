<script setup lang="ts">
import { ref, computed } from 'vue'
import type { ProjectTaskItem } from '@/types/project'
import { statusLabel as taskStatusLabel } from '@/utils/taskConstants'

const props = defineProps<{
  tasks: ProjectTaskItem[]
  dictMap: Record<string, { id: number; dictCode: string; dictLabel: string }[]>
}>()

const emit = defineEmits<{
  view: [task: ProjectTaskItem]
}>()

const boardGroupMode = ref<'category' | 'assignee' | 'dept'>('assignee')

function getGroupKey(task: ProjectTaskItem): string {
  if (boardGroupMode.value === 'assignee') return task.assigneeName || '未指定'
  if (boardGroupMode.value === 'dept') return task.deptName || '未指定'
  return task.taskCategory || ''
}

function getGroupLabel(key: string): string {
  if (!key) return '其他'
  if (boardGroupMode.value === 'assignee') return key
  if (boardGroupMode.value === 'dept') return key
  return props.dictMap['task_category']?.find(d => d.dictCode === key)?.dictLabel ?? key
}

const boardData = computed(() => {
  const parentIds = new Set(props.tasks.filter(t => t.parentId).map(t => t.parentId))
  const leafTasks = props.tasks.filter(t => t.id && !parentIds.has(t.id) && t.nodeType === 1)
  const groups = [...new Set(leafTasks.map(t => getGroupKey(t)))]
  groups.sort((a, b) => {
    if (!a || a === '未指定') return 1
    if (!b || b === '未指定') return -1
    return 0
  })
  return groups.map(g => ({
    category: g,
    tasks: leafTasks.filter(t => getGroupKey(t) === g)
  }))
})

function taskStatusTag(s: number): '' | 'success' | 'warning' | 'info' | 'danger' {
  return (['info', 'primary', 'success'] as any)[s] ?? 'info'
}
</script>

<template>
  <div style="display:flex;gap:8px;margin-bottom:12px">
    <span style="line-height:32px;font-size:14px;font-weight:600">分组方式：</span>
    <el-radio-group v-model="boardGroupMode" size="small">
      <el-radio-button value="assignee">负责人</el-radio-button>
      <el-radio-button value="dept">责任部门</el-radio-button>
      <el-radio-button value="category">任务类别</el-radio-button>
    </el-radio-group>
  </div>
  <div class="board-container">
    <div v-for="col in boardData" :key="col.category" class="board-column">
      <div class="board-column-header">
        <span class="board-column-title">{{ getGroupLabel(col.category) }}</span>
        <el-tag size="small" type="info">{{ col.tasks.length }} 条</el-tag>
      </div>
      <div class="board-column-body">
        <div v-for="task in col.tasks" :key="task.id" class="task-card" style="cursor:pointer" @click="emit('view', task)">
          <div class="task-card-header">
            <span class="task-card-title">{{ task.taskNo }}<br/>{{ task.taskName }}</span>
            <el-tag :type="taskStatusTag(task.status)" size="small">{{ taskStatusLabel(task.status) || '未开始' }}</el-tag>
          </div>
          <div class="task-card-meta">
            <span v-if="task.wbsCode">工序号：{{ task.wbsCode }}</span>
            <span v-if="task.assigneeName">负责人：{{ task.assigneeName }}</span>
          </div>
          <div class="task-card-footer">
            <span class="task-card-date" v-if="task.planFinishDate">截止：{{ task.planFinishDate.slice(0,10) }}</span>
            <span class="task-card-progress">完成进度：{{ task.progressPct }}%</span>
          </div>
          <el-progress :percentage="Number(task.progressPct)" :show-text="false" :stroke-width="4" style="margin-top:6px" />
        </div>
        <div v-if="col.tasks.length === 0" class="board-empty">暂无数据</div>
      </div>
    </div>
  </div>
</template>
