using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataHelpers;

// ==========================================================================
/// <summary>
/// Represents a page of data that we have retrieved from a system.
/// </summary>
/// <typeparam name="T"></typeparam>
public class PagedData<T>
{
  // --------------------------------------------------------------------------------------------------------
  public PagedData()
  {
    PageNumber = 1;
  }

  // --------------------------------------------------------------------------------------------------------
  public PagedData(IEnumerable<T> srcItems, int pageNumber)
    : this(srcItems, pageNumber, PaginationArgs.DEFAULT_PAGE_SIZE)
  { }

  // --------------------------------------------------------------------------------------------------------
  public PagedData(IEnumerable<T> srcItems, int pageNumber_, int pageSize_)
  {
    PageNumber = pageNumber_;
    RequestedPageSize = pageSize_;

    Items = srcItems.Skip((pageNumber_ - 1) * pageSize_).Take(pageSize_).ToArray();

    TotalItems = srcItems.Count();
    TotalPages = (TotalItems / pageSize_) + Math.Sign(TotalItems % pageSize_);
  }


  // --------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Return a PagedData instance without having to whittle down, or sub-select from the given items.
  /// Use this when you only have a single page of data handy, typically this will happen when you are
  /// pulling single pages from a database.
  /// </summary>
  public static PagedData<T> FromSinglePage(IEnumerable<T> items, int pageNumber, int pageSize, int totalItems)
  {
    var res = new PagedData<T>()
    {
      Items = items.ToArray(),
      PageNumber = pageNumber,
      RequestedPageSize = pageSize,
      TotalItems = totalItems,
      TotalPages = (totalItems / pageSize) + Math.Sign(totalItems % pageSize),
    };
    return res;
  }

  /// <summary>
  /// The cursor of this page.
  /// </summary>
  // public string Cursor { get; set; }= default!;

  /// <summary>
  /// All the entries.
  /// </summary>
  public IList<T> Items { get; set; } = new List<T>();

  /// <summary>
  /// The current page number.
  /// </summary>
  public int PageNumber { get; set; }

  /// <summary>
  /// The total number of pages.
  /// </summary>
  public int TotalPages { get; set; }

  /// <summary>
  /// The page size that was requested.
  /// This may be more than the number of actual items.
  /// </summary>
  public int RequestedPageSize { get; set; }

  /// <summary>
  /// The actual page size.
  /// </summary>
  public int PageSize { get { return Items.Count; } }

  /// <summary>
  /// The total number of entries across all pages.
  /// </summary>
  public int TotalItems { get; set; }


  public bool HasPrev { get { return PageNumber > 1 && TotalPages > 1; } }
  public bool HasNext { get { return PageNumber < TotalPages; } }

  ///// <summary>
  ///// Cursor to the previous page of data.
  ///// This is null if there is none.
  ///// </summary>
  //public string? NextCursor { get; set; } = null;

  ///// <summary>
  ///// Cursor to the next page of data.
  ///// This is null if there is none.
  ///// </summary>
  ///// <value></value>
  //public string? PrevCursor { get; set; } = null;
}

// ============================================================================================================================
public class PaginationArgs
{
  public const int DEFAULT_PAGE_SIZE = 10;

  /// <summary>
  /// Indicates that cursor arguments will be respected.
  /// </summary>
  public const int USE_CURSOR_ARGS = -1;

  /// <summary>
  /// The current page number to return results for.
  /// </summary>
  public int Page { get; set; } = 1;

  /// <summary>
  /// The maxinum number of results to return.
  /// </summary>
  public int PageSize { get; set; } = DEFAULT_PAGE_SIZE;

  // ------------------------------------------------------------------------------------------------------------
  public PaginationArgs() { }

  // ------------------------------------------------------------------------------------------------------------
  public PaginationArgs(int page_, int pageSize_)
  {
    this.Page = page_;
    this.PageSize = pageSize_;
  }
}

// ============================================================================================================================
public class PageDataWithCursor<TResult, TCursor>
{
  public int PageSize { get { return PageItems.Count; } }
  public IList<TResult> PageItems { get; set; } = new List<TResult>();
  public bool HasNextPage { get; set; }
  public bool HasPrevPage { get; set; }

  public int TotalPages { get; set; }

  /// <summary>
  /// The starting cursor of this page.
  /// </summary>
  public TCursor StartCursor { get; set; } = default!;

  /// <summary>
  /// The end cursor of this page.
  /// </summary>
  public TCursor EndCursor { get; set; } = default!;
}