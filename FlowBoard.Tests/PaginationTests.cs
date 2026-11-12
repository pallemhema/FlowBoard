using FlowBoard.Web.Components.Shared;

namespace FlowBoard.Tests;

public sealed class PaginationTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(5, false)]
    [InlineData(10, false)]
    [InlineData(11, true)]
    [InlineData(20, true)]
    [InlineData(25, true)]
    public void NavigationIsVisibleOnlyWhenRecordsExceedPageSize(
        int totalRecords,
        bool expected)
    {
        const int pageSize = 10;
        var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

        Assert.Equal(
            expected,
            Pagination.ShouldShowNavigationFor(
                totalRecords,
                pageSize,
                totalPages));
    }
}
