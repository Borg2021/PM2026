<script setup lang="ts">
import { reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import {
  getProjectFinance, saveProjectFinance, savePlanReceipts, saveReceipts, saveInvoices,
} from '@/api/project'
import type { ProjectFinanceInfo, PlanReceiptItem, ReceiptItem, InvoiceItem } from '@/types/project'

const props = defineProps<{
  projectId: number | null
  isReadonly: boolean
}>()

const finance = reactive<ProjectFinanceInfo>({
  taxContractAmount: undefined, taxRate: undefined, currencyType: '',
  paymentMethod: '', currencyAmount: undefined, contributionRate: undefined,
  invoiceRate: undefined, remark: ''
})
const planReceipts = ref<PlanReceiptItem[]>([])
const receipts = ref<ReceiptItem[]>([])
const invoices = ref<InvoiceItem[]>([])
const financeSaving = ref(false)

async function loadFinance() {
  if (!props.projectId) return
  const res = await getProjectFinance(props.projectId)
  if (res.data) {
    Object.assign(finance, {
      taxContractAmount: res.data.taxContractAmount,
      taxRate: res.data.taxRate,
      currencyType: res.data.currencyType ?? '',
      paymentMethod: res.data.paymentMethod ?? '',
      currencyAmount: res.data.currencyAmount,
      contributionRate: res.data.contributionRate,
      invoiceRate: res.data.invoiceRate,
      remark: res.data.remark ?? ''
    })
    planReceipts.value = (res.data.planReceipts ?? []).map(r => ({ ...r }))
    receipts.value = (res.data.receipts ?? []).map(r => ({ ...r }))
    invoices.value = (res.data.invoices ?? []).map(r => ({ ...r }))
  }
}

async function handleSaveFinance() {
  if (!props.projectId) return
  financeSaving.value = true
  try {
    await saveProjectFinance(props.projectId, finance)
    ElMessage.success('财务信息保存成功')
  } finally { financeSaving.value = false }
}

async function handleSavePlanReceipts() {
  if (!props.projectId) return
  financeSaving.value = true
  try {
    await savePlanReceipts(props.projectId, planReceipts.value.map((r, i) => ({ ...r, sortOrder: i + 1 })))
    ElMessage.success('计划收款记录保存成功')
  } finally { financeSaving.value = false }
}

async function handleSaveReceipts() {
  if (!props.projectId) return
  financeSaving.value = true
  try {
    await saveReceipts(props.projectId, receipts.value.map((r, i) => ({ ...r, sortOrder: i + 1 })))
    ElMessage.success('收款记录保存成功')
  } finally { financeSaving.value = false }
}

async function handleSaveInvoices() {
  if (!props.projectId) return
  financeSaving.value = true
  try {
    await saveInvoices(props.projectId, invoices.value.map((r, i) => ({ ...r, sortOrder: i + 1 })))
    ElMessage.success('开票记录保存成功')
  } finally { financeSaving.value = false }
}

defineExpose({ loadFinance })
</script>

<template>
  <el-tab-pane label="财务信息" name="finance" :disabled="!projectId">
    <!-- 合同财务概况 -->
    <el-card shadow="never" class="form-card">
      <template #header>
        <div style="display:flex;justify-content:space-between;align-items:center">
          <span style="font-weight:600">合同财务概况</span>
          <el-button v-if="!isReadonly" type="danger" size="small" :loading="financeSaving" @click="handleSaveFinance">保存</el-button>
        </div>
      </template>
      <el-form label-width="120px" :disabled="isReadonly">
        <el-row :gutter="24">
          <el-col :span="12">
            <el-form-item label="合同市场税金额"><el-input-number v-model="finance.taxContractAmount" :precision="2" style="width:100%" /></el-form-item>
            <el-form-item label="合同货币种"><el-input v-model="finance.currencyType" placeholder="如：人民币" /></el-form-item>
            <el-form-item label="合同货币金额"><el-input-number v-model="finance.currencyAmount" :precision="2" style="width:100%" /></el-form-item>
            <el-form-item label="备注"><el-input v-model="finance.remark" type="textarea" :rows="2" /></el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="税率(%)"><el-input-number v-model="finance.taxRate" :precision="2" :min="0" :max="100" style="width:100%" /></el-form-item>
            <el-form-item label="付款方式"><el-input v-model="finance.paymentMethod" placeholder="如：银行转账" /></el-form-item>
            <el-form-item label="贡献率(%)"><el-input-number v-model="finance.contributionRate" :precision="2" :min="0" :max="100" style="width:100%" /></el-form-item>
            <el-form-item label="开票率(%)"><el-input-number v-model="finance.invoiceRate" :precision="2" :min="0" :max="100" style="width:100%" /></el-form-item>
          </el-col>
        </el-row>
      </el-form>
    </el-card>

    <!-- 计划收款记录 -->
    <el-card shadow="never" class="form-card" style="margin-top:12px">
      <template #header>
        <div style="display:flex;justify-content:space-between;align-items:center">
          <span style="font-weight:600">计划收款记录</span>
          <div style="display:flex;gap:8px">
            <el-button v-if="!isReadonly" size="small" @click="planReceipts.push({ sortOrder: planReceipts.length+1, planAmount: 0, remark: '' })">+ 添加</el-button>
            <el-button v-if="!isReadonly" type="danger" size="small" :loading="financeSaving" @click="handleSavePlanReceipts">保存</el-button>
          </div>
        </div>
      </template>
      <el-table :data="planReceipts" border size="small" style="width:100%">
        <el-table-column type="index" label="序号" width="60" />
        <el-table-column label="计划收款金额" min-width="160">
          <template #default="{ row }">
            <el-input-number v-if="!isReadonly" v-model="row.planAmount" :precision="2" size="small" style="width:100%" />
            <span v-else>{{ row.planAmount }}</span>
          </template>
        </el-table-column>
        <el-table-column label="收款类型" width="130">
          <template #default="{ row }">
            <el-select v-if="!isReadonly" v-model="row.receiptType" size="small" style="width:100%" clearable>
              <el-option label="预付款" value="预付款" />
              <el-option label="进度款" value="进度款" />
              <el-option label="尾款" value="尾款" />
            </el-select>
            <span v-else>{{ row.receiptType }}</span>
          </template>
        </el-table-column>
        <el-table-column label="计划收款日期" width="160">
          <template #default="{ row }">
            <el-date-picker v-if="!isReadonly" v-model="row.planDate" type="date" size="small" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
            <span v-else>{{ row.planDate?.slice(0,10) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="备注" min-width="120">
          <template #default="{ row }">
            <el-input v-if="!isReadonly" v-model="row.remark" size="small" />
            <span v-else>{{ row.remark }}</span>
          </template>
        </el-table-column>
        <el-table-column v-if="!isReadonly" label="操作" width="70">
          <template #default="{ $index }">
            <el-button link style="color:#f56c6c" @click="planReceipts.splice($index, 1)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 收款记录 -->
    <el-card shadow="never" class="form-card" style="margin-top:12px">
      <template #header>
        <div style="display:flex;justify-content:space-between;align-items:center">
          <span style="font-weight:600">收款记录</span>
          <div style="display:flex;gap:8px">
            <el-button v-if="!isReadonly" size="small" @click="receipts.push({ sortOrder: receipts.length+1, actualAmount: 0, remark: '' })">+ 添加</el-button>
            <el-button v-if="!isReadonly" type="danger" size="small" :loading="financeSaving" @click="handleSaveReceipts">保存</el-button>
          </div>
        </div>
      </template>
      <el-table :data="receipts" border size="small" style="width:100%">
        <el-table-column type="index" label="序号" width="60" />
        <el-table-column label="实际收款金额" min-width="160">
          <template #default="{ row }">
            <el-input-number v-if="!isReadonly" v-model="row.actualAmount" :precision="2" size="small" style="width:100%" />
            <span v-else>{{ row.actualAmount }}</span>
          </template>
        </el-table-column>
        <el-table-column label="收款类型" width="130">
          <template #default="{ row }">
            <el-select v-if="!isReadonly" v-model="row.receiptType" size="small" style="width:100%" clearable>
              <el-option label="预付款" value="预付款" /><el-option label="进度款" value="进度款" /><el-option label="尾款" value="尾款" />
            </el-select>
            <span v-else>{{ row.receiptType }}</span>
          </template>
        </el-table-column>
        <el-table-column label="收款时间" width="160">
          <template #default="{ row }">
            <el-date-picker v-if="!isReadonly" v-model="row.receiptTime" type="date" size="small" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
            <span v-else>{{ row.receiptTime?.slice(0,10) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="备注" min-width="120">
          <template #default="{ row }">
            <el-input v-if="!isReadonly" v-model="row.remark" size="small" />
            <span v-else>{{ row.remark }}</span>
          </template>
        </el-table-column>
        <el-table-column v-if="!isReadonly" label="操作" width="70">
          <template #default="{ $index }">
            <el-button link style="color:#f56c6c" @click="receipts.splice($index, 1)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <!-- 开票记录 -->
    <el-card shadow="never" class="form-card" style="margin-top:12px">
      <template #header>
        <div style="display:flex;justify-content:space-between;align-items:center">
          <span style="font-weight:600">开票记录</span>
          <div style="display:flex;gap:8px">
            <el-button v-if="!isReadonly" size="small" @click="invoices.push({ sortOrder: invoices.length+1, invoiceAmount: 0, remark: '' })">+ 添加</el-button>
            <el-button v-if="!isReadonly" type="danger" size="small" :loading="financeSaving" @click="handleSaveInvoices">保存</el-button>
          </div>
        </div>
      </template>
      <el-table :data="invoices" border size="small" style="width:100%">
        <el-table-column type="index" label="序号" width="60" />
        <el-table-column label="发票金额" min-width="130">
          <template #default="{ row }">
            <el-input-number v-if="!isReadonly" v-model="row.invoiceAmount" :precision="2" size="small" style="width:100%" />
            <span v-else>{{ row.invoiceAmount }}</span>
          </template>
        </el-table-column>
        <el-table-column label="发票比例(%)" width="120">
          <template #default="{ row }">
            <el-input-number v-if="!isReadonly" v-model="row.invoiceRate" :precision="2" :min="0" :max="100" size="small" style="width:100%" />
            <span v-else>{{ row.invoiceRate }}</span>
          </template>
        </el-table-column>
        <el-table-column label="开票时间" width="150">
          <template #default="{ row }">
            <el-date-picker v-if="!isReadonly" v-model="row.invoiceTime" type="date" size="small" style="width:100%" value-format="YYYY-MM-DDTHH:mm:ss" />
            <span v-else>{{ row.invoiceTime?.slice(0,10) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="票号" width="150">
          <template #default="{ row }">
            <el-input v-if="!isReadonly" v-model="row.invoiceNo" size="small" />
            <span v-else>{{ row.invoiceNo }}</span>
          </template>
        </el-table-column>
        <el-table-column label="备注" min-width="100">
          <template #default="{ row }">
            <el-input v-if="!isReadonly" v-model="row.remark" size="small" />
            <span v-else>{{ row.remark }}</span>
          </template>
        </el-table-column>
        <el-table-column v-if="!isReadonly" label="操作" width="70">
          <template #default="{ $index }">
            <el-button link style="color:#f56c6c" @click="invoices.splice($index, 1)">删除</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </el-tab-pane>
</template>
