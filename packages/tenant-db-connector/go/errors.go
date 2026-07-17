package tenantdbconnector

import "errors"

var (
	// ErrTenantNotFound 表示 tenantCode 不存在，SsoGetter 实现应在这种情况下返回本错误，
	// 本 SDK 据此判断是否值得重试/回源，而不是把"租户不存在"当成瞬时故障重试。
	ErrTenantNotFound = errors.New("tenantdbconnector: tenant not found")

	// ErrClosed 表示 Connector 已被 Close，之后的调用不再可用。
	ErrClosed = errors.New("tenantdbconnector: connector is closed")
)
