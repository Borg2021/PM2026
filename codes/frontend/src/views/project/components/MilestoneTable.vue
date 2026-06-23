<script setup lang="ts">
import { computed } from 'vue'
import type { ProjectTaskItem } from '@/types/project'

const props = withDefaults(defineProps<{
  tasks: ProjectTaskItem[]
  isReadonly?: boolean
}>(), {
  isReadonly: false
})

const emit = defineEmits<{
  edit: [task: ProjectTaskItem]
  view: [task: ProjectTaskItem]
}>()

const milestoneTasks = computed(() =>
  props.tasks.filter(t => t.nodeType === 2).sort((a, b) => (a.taskNo || '').localeCompare(b.taskNo || '', undefined, { numeric: true }))
)
</script>

<template>
  <el-card shadow="never" class="form-card">
    <template #header><span style="font-weight:600">里程碑列表</span></template>
    <el-table :data="milestoneTasks" border size="small" style="width:100%" max-height="calc(100vh - 350px)" empty-text="暂无里程碑数据">
      <el-table-column type="index" label="序号" width="60" fixed="left" />
      <el-table-column label="任务编号" width="180" prop="taskNo" />
      <el-table-column label="里程碑名称" min-width="200" prop="taskName" show-overflow-tooltip />
      <el-table-column label="状态" width="100" align="center">
        <template #default="{ row }">
          <el-tag :type="row.status === 2 ? 'success' : row.status === 1 ? 'primary' : 'info'" size="small">
            {{ row.status === 2 ? '已完成' : row.status === 1 ? '进行中' : '未开始' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column label="计划完成" width="110" align="center">
        <template #default="{ row }">{{ row.planFinishDate?.slice(0, 10) ?? '-' }}</template>
      </el-table-column>
      <el-table-column label="实际完成" width="110" align="center">
        <template #default="{ row }">{{ row.actualFinishDate?.slice(0, 10) ?? '-' }}</template>
      </el-table-column>
      <el-table-column label="进度" width="100" align="center">
        <template #default="{ row }">
          <el-progress :percentage="row.progressPct ?? 0" :status="row.progressPct >= 100 ? 'success' : ''" />
        </template>
      </el-table-column>
      <el-table-column label="责任人" width="120" prop="assigneeName" show-overflow-tooltip />
      <el-table-column label="操作" width="120" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" size="small" @click="emit('view', row)">查看</el-button>
          <el-button v-if="!isReadonly" link type="primary" size="small" @click="emit('edit', row)">编辑</el-button>
        </template>
      </el-table-column>
    </el-table>
  </el-card>
</template>
