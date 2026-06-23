<script setup lang="ts">
import { computed } from 'vue'

interface ScopeItem {
  sortOrder: number
  scopeName: string
  scopeDesc: string
}

const props = defineProps<{
  modelValue: ScopeItem[]
  readonly?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: ScopeItem[]]
}>()

const items = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

function add() {
  const arr = [...items.value, { sortOrder: items.value.length + 1, scopeName: '', scopeDesc: '' }]
  emit('update:modelValue', arr)
}

function remove(idx: number) {
  const arr = [...items.value]
  arr.splice(idx, 1)
  emit('update:modelValue', arr)
}
</script>

<template>
  <el-card shadow="never" class="form-card" style="margin-top:12px">
    <template #header>
      <div style="display:flex;justify-content:space-between;align-items:center">
        <span style="font-weight:600">项目范围</span>
        <el-button v-if="!readonly" type="danger" size="small" @click="add">+ 添加范围</el-button>
      </div>
    </template>
    <el-table :data="items" border size="small" style="width:100%" empty-text="暂无项目范围">
      <el-table-column type="index" label="序号" width="55" />
      <el-table-column label="项目范围" width="324">
        <template #default="{ row }">
          <el-input v-if="!readonly" v-model="row.scopeName" size="small" placeholder="请输入项目范围" />
          <span v-else>{{ row.scopeName }}</span>
        </template>
      </el-table-column>
      <el-table-column label="范围说明" min-width="240">
        <template #default="{ row }">
          <el-input v-if="!readonly" v-model="row.scopeDesc" size="small" placeholder="请输入范围说明" />
          <span v-else>{{ row.scopeDesc }}</span>
        </template>
      </el-table-column>
      <el-table-column v-if="!readonly" label="操作" width="70">
        <template #default="{ $index }">
          <el-button link style="color:#f56c6c" @click="remove($index)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>
  </el-card>
</template>
