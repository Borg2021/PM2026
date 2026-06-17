<script setup lang="ts">
import type { Milestone } from '@/types/template'

const props = defineProps<{
  modelValue: Milestone[]
  readonly: boolean
}>()

const emit = defineEmits<{
  (e: 'update:modelValue', value: Milestone[]): void
}>()

function addRow(): void {
  const newItem: Milestone = {
    milestoneCode: '',
    milestoneName: '',
    sortOrder: props.modelValue.length + 1,
    remark: ''
  }
  emit('update:modelValue', [...props.modelValue, newItem])
}

function deleteRow(index: number): void {
  const next = [...props.modelValue]
  next.splice(index, 1)
  emit('update:modelValue', next)
}

function updateField(index: number, field: 'milestoneCode' | 'milestoneName' | 'remark', value: string): void {
  const next = [...props.modelValue]
  next[index] = { ...next[index], [field]: value }
  emit('update:modelValue', next)
}
</script>

<template>
  <div class="milestone-table">
    <div v-if="!readonly" class="table-toolbar">
      <el-button type="danger" size="small" @click="addRow">+ 新建里程碑</el-button>
    </div>
    <el-table :data="modelValue" border stripe size="small" empty-text="暂无数据">
      <el-table-column type="index" label="序号" width="55" />
      <el-table-column label="里程碑编号" min-width="150">
        <template #default="{ row, $index }">
          <el-input
            v-if="!readonly"
            :model-value="row.milestoneCode"
            placeholder="请输入编号"
            size="small"
            @input="(val: string) => updateField($index, 'milestoneCode', val)"
          />
          <span v-else>{{ row.milestoneCode }}</span>
        </template>
      </el-table-column>
      <el-table-column label="里程碑名称" min-width="180">
        <template #default="{ row, $index }">
          <el-input
            v-if="!readonly"
            :model-value="row.milestoneName"
            placeholder="请输入名称"
            size="small"
            @input="(val: string) => updateField($index, 'milestoneName', val)"
          />
          <span v-else>{{ row.milestoneName }}</span>
        </template>
      </el-table-column>
      <el-table-column label="备注" min-width="200">
        <template #default="{ row, $index }">
          <el-input
            v-if="!readonly"
            :model-value="row.remark"
            placeholder="请输入备注"
            size="small"
            @input="(val: string) => updateField($index, 'remark', val)"
          />
          <span v-else>{{ row.remark }}</span>
        </template>
      </el-table-column>
      <el-table-column v-if="!readonly" label="操作" width="65" fixed="right">
        <template #default="{ $index }">
          <el-button type="danger" link size="small" @click="deleteRow($index)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>
  </div>
</template>

<style scoped>
.milestone-table .table-toolbar {
  margin-bottom: 12px;
  text-align: right;
}
</style>
