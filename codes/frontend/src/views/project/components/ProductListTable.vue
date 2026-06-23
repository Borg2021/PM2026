<script setup lang="ts">
import { computed } from 'vue'
import type { ProductItem } from '@/types/project'

const props = defineProps<{
  modelValue: ProductItem[]
  readonly?: boolean
  dictMap: Record<string, { id: number; dictCode: string; dictLabel: string }[]>
}>()

const emit = defineEmits<{
  'update:modelValue': [value: ProductItem[]]
}>()

const products = computed({
  get: () => props.modelValue,
  set: (val) => emit('update:modelValue', val)
})

function getDictLabel(dictType: string, code: string | undefined): string {
  if (!code) return ''
  return props.dictMap[dictType]?.find(d => d.dictCode === code)?.dictLabel ?? code
}

function add() {
  const arr = [...products.value, { sortOrder: products.value.length + 1, productType: '', quantity: 1, remark: '' }]
  emit('update:modelValue', arr)
}

function remove(idx: number) {
  const arr = [...products.value]
  arr.splice(idx, 1)
  emit('update:modelValue', arr)
}
</script>

<template>
  <el-card shadow="never" class="form-card" style="margin-top:12px">
    <template #header>
      <div style="display:flex;justify-content:space-between;align-items:center">
        <span style="font-weight:600">产品列表</span>
        <el-button v-if="!readonly" type="danger" size="small" @click="add">+ 添加产品</el-button>
      </div>
    </template>
    <el-table :data="products" border size="small" style="width:100%">
      <el-table-column type="index" label="序号" width="55" />
      <el-table-column label="产品类型" width="150">
        <template #default="{ row }">
          <el-select v-if="!readonly" v-model="row.productType" placeholder="请选择" size="small" clearable style="width:100%">
            <el-option v-for="item in (dictMap['product_type'] || [])" :key="item.dictCode" :label="item.dictLabel" :value="item.dictCode" />
          </el-select>
          <span v-else>{{ getDictLabel('product_type', row.productType) }}</span>
        </template>
      </el-table-column>
      <el-table-column label="数量" width="80">
        <template #default="{ row }">
          <el-input-number v-if="!readonly" v-model="row.quantity" :min="1" size="small" style="width:100%" />
          <span v-else>{{ row.quantity }}</span>
        </template>
      </el-table-column>
      <el-table-column label="计划交付日期" width="150">
        <template #default="{ row }">
          <el-date-picker v-if="!readonly" v-model="row.plannedDelivery" type="date" size="small" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
          <span v-else>{{ row.plannedDelivery ? row.plannedDelivery.slice(0,10) : '' }}</span>
        </template>
      </el-table-column>
      <el-table-column label="备注" min-width="120">
        <template #default="{ row }">
          <el-input v-if="!readonly" v-model="row.remark" size="small" />
          <span v-else>{{ row.remark }}</span>
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
