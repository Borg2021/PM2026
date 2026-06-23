<script setup lang="ts">
const props = defineProps<{
  projectId: number | null
}>()

// 生命周期展示组件 - 展示项目状态流转历史
// 当前仅展示状态标签，后续可扩展为完整的状态流转图
</script>

<template>
  <!-- 生命周期展示预留插槽 -->
  <div class="lifecycle-placeholder">
    <span style="color:#909399;font-size:13px">生命周期功能开发中...</span>
  </div>
</template>

<style scoped>
.lifecycle-placeholder {
  padding: 24px;
  text-align: center;
}
</style>
