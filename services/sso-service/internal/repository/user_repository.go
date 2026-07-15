package repository

import (
	"context"
	"errors"

	"gorm.io/gorm"

	"github.com/company/sso-service/internal/model"
)

var ErrUserNotFound = errors.New("repository: user not found")

// UserSortField 是待审核用户列表接口的可排序字段白名单（规范第 16.4.2 章），
// 由本资源自行声明，不与其他资源共享。
type UserSortField string

const (
	UserSortFieldID        UserSortField = "id"
	UserSortFieldCreatedAt UserSortField = "createdAt"
)

// userSortColumns 把对外的驼峰字段名映射到实际列名，避免请求参数直接拼接进 SQL。
var userSortColumns = map[UserSortField]string{
	UserSortFieldID:        "id",
	UserSortFieldCreatedAt: "created_at",
}

type UserRepository interface {
	Create(ctx context.Context, u *model.User) error
	FindByUsername(ctx context.Context, username string) (*model.User, error)
	FindByEmail(ctx context.Context, email string) (*model.User, error)
	FindByID(ctx context.Context, id uint64) (*model.User, error)
	ApproveReview(ctx context.Context, id uint64, reviewedBy uint64) error
	// RejectReview 幂等地拒绝审核并软删除该用户，允许其后续用同一 username/email 重新注册。
	// 拒绝不可撤销：软删除后该用户不再被 FindByID/FindByUsername/FindByEmail/ListByReviewStatus 查到。
	RejectReview(ctx context.Context, id uint64, reviewedBy uint64) error
	// ListByReviewStatus 分页查询指定审核状态的用户，sortBy 已在 Service 层校验落到白名单，
	// 这里只负责按（列名, 排序方向）拼查询。
	ListByReviewStatus(ctx context.Context, reviewStatus string, page, pageSize int, sortField UserSortField, sortOrder string) ([]model.User, int64, error)
}

type userRepository struct {
	db *gorm.DB
}

func NewUserRepository(db *gorm.DB) UserRepository {
	return &userRepository{db: db}
}

func (r *userRepository) Create(ctx context.Context, u *model.User) error {
	return r.db.WithContext(ctx).Create(u).Error
}

// FindByUsername/FindByEmail 显式排除已软删除（拒绝审核）的用户，使被拒绝的 username/email
// 能被后续注册重新使用——DeletedAt 是普通 *time.Time，不是 gorm.DeletedAt，
// 不会被 GORM 自动注入查询过滤，必须每处手动加 deleted_at IS NULL。
func (r *userRepository) FindByUsername(ctx context.Context, username string) (*model.User, error) {
	var u model.User
	err := r.db.WithContext(ctx).Where("username = ? AND deleted_at IS NULL", username).First(&u).Error
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return nil, ErrUserNotFound
	}
	if err != nil {
		return nil, err
	}
	return &u, nil
}

func (r *userRepository) FindByEmail(ctx context.Context, email string) (*model.User, error) {
	var u model.User
	err := r.db.WithContext(ctx).Where("email = ? AND deleted_at IS NULL", email).First(&u).Error
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return nil, ErrUserNotFound
	}
	if err != nil {
		return nil, err
	}
	return &u, nil
}

func (r *userRepository) FindByID(ctx context.Context, id uint64) (*model.User, error) {
	var u model.User
	err := r.db.WithContext(ctx).Where("id = ? AND deleted_at IS NULL", id).First(&u).Error
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return nil, ErrUserNotFound
	}
	if err != nil {
		return nil, err
	}
	return &u, nil
}

// ApproveReview 幂等：重复调用同一个已审核用户不会报错，只是重复写入相同的值。
// 只更新未软删除的行——已被拒绝（软删除）的用户不可能再被 approve，视为不存在。
func (r *userRepository) ApproveReview(ctx context.Context, id uint64, reviewedBy uint64) error {
	result := r.db.WithContext(ctx).Model(&model.User{}).
		Where("id = ? AND deleted_at IS NULL", id).
		Updates(map[string]any{
			"review_status": model.UserReviewStatusApproved,
			"reviewed_by":   reviewedBy,
		})
	if result.Error != nil {
		return result.Error
	}
	if result.RowsAffected == 0 {
		return ErrUserNotFound
	}
	return nil
}

// RejectReview 幂等地把用户标记为已拒绝并软删除。重复调用同一个已软删除的用户会返回
// ErrUserNotFound（该行已不满足 deleted_at IS NULL），这是预期行为：拒绝不可撤销，
// 不支持"拒绝之后再拒绝一次"的语义。
func (r *userRepository) RejectReview(ctx context.Context, id uint64, reviewedBy uint64) error {
	result := r.db.WithContext(ctx).Model(&model.User{}).
		Where("id = ? AND deleted_at IS NULL", id).
		Updates(map[string]any{
			"review_status": model.UserReviewStatusRejected,
			"reviewed_by":   reviewedBy,
			"deleted_at":    gorm.Expr("NOW(6)"),
		})
	if result.Error != nil {
		return result.Error
	}
	if result.RowsAffected == 0 {
		return ErrUserNotFound
	}
	return nil
}

func (r *userRepository) ListByReviewStatus(
	ctx context.Context, reviewStatus string, page, pageSize int, sortField UserSortField, sortOrder string,
) ([]model.User, int64, error) {
	column, ok := userSortColumns[sortField]
	if !ok {
		column = userSortColumns[UserSortFieldCreatedAt]
	}

	query := r.db.WithContext(ctx).Model(&model.User{}).Where("review_status = ?", reviewStatus)
	// rejected 用户本身就是靠软删除表示的（见 RejectReview），查这个状态时不能再排除软删除的行，
	// 否则"查已拒绝用户"永远是空结果；查 pending/approved 时才需要排除软删除行
	// （避免已拒绝但残留旧 review_status 快照的边界情况，正常流程不会出现，这里是防御性写法）。
	if reviewStatus != model.UserReviewStatusRejected {
		query = query.Where("deleted_at IS NULL")
	}

	var total int64
	if err := query.Count(&total).Error; err != nil {
		return nil, 0, err
	}

	var users []model.User
	err := query.
		Order(column + " " + sortOrder).
		Offset((page - 1) * pageSize).
		Limit(pageSize).
		Find(&users).Error
	if err != nil {
		return nil, 0, err
	}

	return users, total, nil
}
