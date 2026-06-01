using Avalonia.Plugin.Shared;
using Avalonia.Plugin.Shared.Attributes;
using Avalonia.Plugin.ProDataGrid.Models;
using Avalonia.Plugin.ProDataGrid.Pages;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Avalonia.Plugin.ProDataGrid.ViewModels;

[NavigationItem("KeyColumnTypes")]
[Menu("NAV_ColumnTypes", "KeyColumnTypes", "NAV_ProDataGrid")]
[ViewMap(typeof(ColumnTypesDemo))]
public partial class ColumnTypesDemoViewModel : ObservableObject
{
    private static readonly string[] FirstNames =
    [
        "张伟", "王芳", "李娜", "刘洋", "陈静", "杨磊", "赵敏", "黄强",
        "周婷", "吴鹏", "徐明", "孙丽", "马超", "朱军", "胡雪", "郭涛",
        "林峰", "何欣", "罗杰", "梁宇"
    ];
    private static readonly string[] LastNames =
    [
        "王", "李", "张", "刘", "陈", "杨", "赵", "黄",
        "周", "吴", "徐", "孙", "马", "朱", "胡", "郭"
    ];
    private static readonly string[] Departments =
    [
        "研发部", "市场部", "销售部", "人力资源部", "财务部", "运营部", "法务部", "客服部",
        "产品部", "设计部", "质量部", "行政部"
    ];
    private static readonly string[] Positions =
    [
        "技术总监", "高级工程师", "初级工程师", "项目经理", "实习生",
        "部门主管", "数据分析师", "产品专员", "UI设计师", "测试工程师",
        "架构师", "运维工程师", "前端开发", "后端开发", "全栈开发"
    ];
    private static readonly string[] Cities =
    [
        "上海", "北京", "深圳", "杭州", "成都", "武汉", "南京", "苏州",
        "广州", "重庆", "西安", "长沙", "天津", "青岛", "大连", "厦门"
    ];
    private static readonly string[] Notes =
    [
        "团队核心成员，具备出色的领导力和沟通能力",
        "本季度绩效优秀，已获晋升资格",
        "正在负责Q3关键项目的交付工作",
        "远程办公中，每周三到公司",
        "正在指导三名新入职员工",
        "已申请下月休年假",
        "跨部门协作表现突出，获季度之星",
        "新入职员工，目前处于试用期",
        "技术分享会主讲人，每月组织一次",
        "参与开源项目贡献，公司形象大使",
        "已完成PMP认证，具备项目管理资质",
        "正在攻读在职研究生学位",
        "本年度最佳新人奖候选人",
        "负责客户培训和技术支持工作",
        "即将调往深圳分部工作"
    ];
    private static readonly Random _random = new();
    private int _nextId = 1;

    public ObservableCollection<Employee> Employees { get; }

    public ObservableCollection<string> DepartmentOptions { get; } = new(Departments);

    [ObservableProperty] private Employee? _selectedEmployee;

    public ColumnTypesDemoViewModel()
    {
        Employees = new ObservableCollection<Employee>(GenerateEmployees(40));
    }

    [RelayCommand]
    private void AddEmployee()
    {
        Employees.Add(CreateRandomEmployee());
    }

    [RelayCommand]
    private void ResetData()
    {
        Employees.Clear();
        _nextId = 1;
        foreach (var emp in GenerateEmployees(40))
            Employees.Add(emp);
    }

    [RelayCommand]
    private void Save()
    {
    }

    private List<Employee> GenerateEmployees(int count)
    {
        var list = new List<Employee>();
        for (int i = 0; i < count; i++)
            list.Add(CreateRandomEmployee());
        return list;
    }

    private Employee CreateRandomEmployee()
    {
        var firstName = FirstNames[_random.Next(FirstNames.Length)];
        var lastName = LastNames[_random.Next(LastNames.Length)];
        var dept = Departments[_random.Next(Departments.Length)];
        var position = Positions[_random.Next(Positions.Length)];
        var city = Cities[_random.Next(Cities.Length)];
        var salary = position switch
        {
            "技术总监" => Math.Round(_random.NextDouble() * 30000 + 50000, 2),
            "架构师" => Math.Round(_random.NextDouble() * 25000 + 40000, 2),
            "部门主管" => Math.Round(_random.NextDouble() * 20000 + 35000, 2),
            "项目经理" => Math.Round(_random.NextDouble() * 15000 + 30000, 2),
            "高级工程师" => Math.Round(_random.NextDouble() * 12000 + 25000, 2),
            "全栈开发" => Math.Round(_random.NextDouble() * 10000 + 22000, 2),
            "前端开发" or "后端开发" => Math.Round(_random.NextDouble() * 8000 + 18000, 2),
            "UI设计师" => Math.Round(_random.NextDouble() * 8000 + 16000, 2),
            "数据分析师" => Math.Round(_random.NextDouble() * 7000 + 15000, 2),
            "产品专员" => Math.Round(_random.NextDouble() * 6000 + 14000, 2),
            "测试工程师" or "运维工程师" => Math.Round(_random.NextDouble() * 6000 + 13000, 2),
            "初级工程师" => Math.Round(_random.NextDouble() * 5000 + 10000, 2),
            "实习生" => Math.Round(_random.NextDouble() * 2000 + 4000, 2),
            _ => Math.Round(_random.NextDouble() * 10000 + 15000, 2)
        };
        var isActive = _random.NextDouble() > 0.15;
        var hireDate = DateTime.Now.AddDays(-_random.Next(30, 3650));
        var rating = position switch
        {
            "技术总监" or "架构师" => _random.Next(4, 6),
            "部门主管" or "项目经理" => _random.Next(3, 6),
            "实习生" => _random.Next(1, 4),
            _ => _random.Next(1, 6)
        };
        var email = $"{firstName.ToLower()}_{lastName.ToLower()}@company.com";
        var note = Notes[_random.Next(Notes.Length)];

        return new Employee(
            _nextId++,
            firstName,
            lastName,
            dept,
            position,
            salary,
            isActive,
            hireDate,
            rating,
            email,
            city,
            note
        );
    }
}
