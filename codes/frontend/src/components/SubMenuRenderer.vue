<script setup lang="ts">
export interface SubMenuItem {
  path: string
  title?: string
  name?: string
  icon?: string
  badge?: number | string
  children?: SubMenuItem[]
}

defineProps<{ item: SubMenuItem }>()
</script>

<template>
  <el-sub-menu v-if="item.children && item.children.length > 0" :index="item.path">
    <template #title>
      <component :is="item.icon" v-if="item.icon" style="width:1em;height:1em;flex-shrink:0;display:inline-block;vertical-align:middle;margin-right:5px" />
      <span>{{ item.name || item.title }}</span>
      <span v-if="item.badge != null" class="menu-badge">({{ item.badge }})</span>
    </template>
    <SubMenuRenderer v-for="child in item.children" :key="child.path" :item="child" />
  </el-sub-menu>
  <el-menu-item v-else :index="item.path">
    <component :is="item.icon" v-if="item.icon" style="width:1em;height:1em;flex-shrink:0;display:inline-block;vertical-align:middle;margin-right:5px" />
    <span>{{ item.name || item.title }}</span>
    <span v-if="item.badge != null" class="menu-badge">({{ item.badge }})</span>
  </el-menu-item>
</template>

<style scoped>
.menu-badge {
  color: #f56c6c;
  margin-left: 4px;
}
</style>
