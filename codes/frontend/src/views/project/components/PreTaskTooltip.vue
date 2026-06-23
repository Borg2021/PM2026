<script setup lang="ts">
import { computed } from 'vue'
import type { ProjectTaskItem } from '@/types/project'
import { parsePreTaskCodes } from '@/utils/preTaskHelpers'

const props = defineProps<{
  visible: boolean
  x: number
  y: number
  task: ProjectTaskItem | null
  allTasks: ProjectTaskItem[]
}>()

const emit = defineEmits<{
  'update:visible': [value: boolean]
  navigate: [taskId: number]
}>()

const taskIdMap = computed(() => {
  const map = new Map<number, ProjectTaskItem>()
  for (const t of props.allTasks) {
    if (t.id) map.set(t.id, t)
  }
  return map
})

function handleMouseEnter() {
  emit('update:visible', true)
}

function handleMouseLeave() {
  emit('update:visible', false)
}

function handleClick(taskId: number) {
  emit('navigate', taskId)
}
</script>

<template>
  <Teleport to="body">
    <div
      v-if="visible && task"
      class="pover"
      :style="{ left: x + 'px', top: y + 'px' }"
      @mouseenter="handleMouseEnter"
      @mouseleave="handleMouseLeave"
    >
      <div
        v-for="seg in parsePreTaskCodes(task.preTaskCodes)"
        :key="seg.taskId"
        class="pitem"
        @click="handleClick(seg.taskId)"
      >
        <span class="pno">{{ taskIdMap.get(seg.taskId)?.taskNo ?? seg.taskId }}</span>
        <span class="pnm">
          <span>{{ taskIdMap.get(seg.taskId)?.taskName ?? '（已删除）' }}</span>
          <span class="pprogress">{{ (taskIdMap.get(seg.taskId)?.progressPct ?? 0) + '%' }}</span>
        </span>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.pover {
  position: fixed; z-index: 99999;
  min-width: 200px; max-width: 380px; max-height: 300px; overflow-y: auto;
  background: #fff;
  border: 1px solid #d9d9d9; border-radius: 6px;
  box-shadow: 0 6px 20px rgba(0,0,0,0.15);
  padding: 4px 0;
}
.pitem {
  display: flex; align-items: center; gap: 10px;
  padding: 8px 14px; cursor: pointer;
  font-size: 13px;
}
.pitem:hover { background: #e6f4ff; }
.pitem + .pitem { border-top: 1px solid #f0f0f0; }
.pno {
  font-weight: 700; color: #409eff; white-space: nowrap; flex-shrink: 0; min-width: 32px;
}
.pnm {
  color: #303133; overflow: hidden; display: flex; align-items: center; gap: 6px; min-width: 0; flex: 1;
}
.pnm > span:first-child {
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap; min-width: 0;
}
.pprogress {
  flex-shrink: 0; font-size: 12px; color: #e74c3c; background: #fff2f0; border-radius: 4px; padding: 1px 6px; font-weight: 600;
}
</style>
