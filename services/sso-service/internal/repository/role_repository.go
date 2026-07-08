package repository

import (
	"context"
	"errors"

	mysqldriver "github.com/go-sql-driver/mysql"
	"gorm.io/gorm"

	"github.com/company/sso-service/internal/model"
)

const mysqlErrDuplicateEntry = 1062

func isDuplicateEntryErr(err error) bool {
	var mysqlErr *mysqldriver.MySQLError
	return errors.As(err, &mysqlErr) && mysqlErr.Number == mysqlErrDuplicateEntry
}

var (
	ErrRoleNotFound     = errors.New("repository: role not found")
	ErrRoleNameTaken    = errors.New("repository: role name already exists")
	ErrRoleAlreadyOwned = errors.New("repository: user already has this role")
)

type RoleRepository interface {
	Create(ctx context.Context, r *model.Role) error
	FindByName(ctx context.Context, name string) (*model.Role, error)
	FindByID(ctx context.Context, id uint64) (*model.Role, error)
	ListAll(ctx context.Context) ([]model.Role, error)

	AssignToUser(ctx context.Context, userID, roleID uint64) error
	RemoveFromUser(ctx context.Context, userID, roleID uint64) error
	ListByUserID(ctx context.Context, userID uint64) ([]model.Role, error)
}

type roleRepository struct {
	db *gorm.DB
}

func NewRoleRepository(db *gorm.DB) RoleRepository {
	return &roleRepository{db: db}
}

func (r *roleRepository) Create(ctx context.Context, role *model.Role) error {
	err := r.db.WithContext(ctx).Create(role).Error
	if isDuplicateEntryErr(err) {
		return ErrRoleNameTaken
	}
	return err
}

func (r *roleRepository) FindByName(ctx context.Context, name string) (*model.Role, error) {
	var role model.Role
	err := r.db.WithContext(ctx).Where("name = ?", name).First(&role).Error
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return nil, ErrRoleNotFound
	}
	if err != nil {
		return nil, err
	}
	return &role, nil
}

func (r *roleRepository) FindByID(ctx context.Context, id uint64) (*model.Role, error) {
	var role model.Role
	err := r.db.WithContext(ctx).Where("id = ?", id).First(&role).Error
	if errors.Is(err, gorm.ErrRecordNotFound) {
		return nil, ErrRoleNotFound
	}
	if err != nil {
		return nil, err
	}
	return &role, nil
}

func (r *roleRepository) ListAll(ctx context.Context) ([]model.Role, error) {
	var roles []model.Role
	if err := r.db.WithContext(ctx).Order("id ASC").Find(&roles).Error; err != nil {
		return nil, err
	}
	return roles, nil
}

func (r *roleRepository) AssignToUser(ctx context.Context, userID, roleID uint64) error {
	link := model.UserRole{UserID: userID, RoleID: roleID}
	err := r.db.WithContext(ctx).Create(&link).Error
	if isDuplicateEntryErr(err) {
		return ErrRoleAlreadyOwned
	}
	return err
}

func (r *roleRepository) RemoveFromUser(ctx context.Context, userID, roleID uint64) error {
	return r.db.WithContext(ctx).
		Where("user_id = ? AND role_id = ?", userID, roleID).
		Delete(&model.UserRole{}).Error
}

func (r *roleRepository) ListByUserID(ctx context.Context, userID uint64) ([]model.Role, error) {
	var roles []model.Role
	err := r.db.WithContext(ctx).
		Joins("JOIN user_roles ON user_roles.role_id = roles.id").
		Where("user_roles.user_id = ?", userID).
		Order("roles.id ASC").
		Find(&roles).Error
	if err != nil {
		return nil, err
	}
	return roles, nil
}
