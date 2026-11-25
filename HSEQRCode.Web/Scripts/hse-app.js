(function () {
  "use strict";

  function initSelect2() {
    if (typeof window.jQuery === "undefined" || typeof window.jQuery.fn.select2 !== "function") {
      return;
    }

    window.jQuery(".hse-select2").each(function () {
      var $el = window.jQuery(this);
      $el.select2({
        dir: "rtl",
        width: "100%",
        placeholder: $el.attr("multiple") ? "یک یا چند مورد را انتخاب کنید..." : "انتخاب کنید...",
        allowClear: !$el.prop("required")
      });
    });
  }

  function initJalaliDatepicker() {
    if (typeof window.jalaliDatepicker !== "undefined" && typeof window.jalaliDatepicker.startWatch === "function") {
      window.jalaliDatepicker.startWatch({
        persianDigits: false,
        autoShow: true,
        autoHide: true
      });
    }
  }

  function initFileDropzones() {
    var zones = document.querySelectorAll(".js-file-dropzone");
    zones.forEach(function (zone) {
      var input = zone.querySelector('input[type="file"]');
      var label = zone.querySelector(".js-file-dropzone-label");
      if (!input || !label) return;

      input.addEventListener("change", function () {
        if (input.files && input.files.length > 0) {
          label.textContent = "فایل انتخاب‌شده: " + input.files[0].name;
        }
      });

      zone.addEventListener("dragover", function (e) {
        e.preventDefault();
        zone.classList.add("is-dragover");
      });

      zone.addEventListener("dragleave", function () {
        zone.classList.remove("is-dragover");
      });

      zone.addEventListener("drop", function () {
        zone.classList.remove("is-dragover");
      });
    });
  }

  function initIndicatorLookup() {
    var forms = document.querySelectorAll(".js-indicator-lookup-form");
    forms.forEach(function (form) {
      form.addEventListener("submit", function (e) {
        var jsonUrl = form.getAttribute("data-lookup-json-url");
        var input = form.querySelector(".js-indicator-lookup-input");
        var feedback = form.parentElement.querySelector(".js-lookup-feedback");

        if (!jsonUrl || !input || !window.fetch) {
          return;
        }

        var indicator = (input.value || "").trim();
        if (!indicator) {
          return;
        }

        e.preventDefault();

        fetch(jsonUrl + "?indicator=" + encodeURIComponent(indicator), {
          headers: { "Accept": "application/json" }
        })
          .then(function (res) { return res.json(); })
          .then(function (data) {
            if (data && data.found && data.redirectUrl) {
              window.location.href = data.redirectUrl;
              return;
            }

            if (feedback) {
              feedback.className = "js-lookup-feedback alert alert-danger py-2 px-3 small mt-3";
              feedback.textContent = (data && data.message) ? data.message : "تجهیزی با این شناسه یافت نشد.";
            } else {
              form.submit();
            }
          })
          .catch(function () {
            form.submit();
          });
      });
    });
  }

  function initAdminCreateDataSource() {
    var eavToggle = document.querySelector(".js-eav-toggle");
    var eavPanel = document.getElementById("eavColumnsPanel");
    if (eavToggle && eavPanel) {
      var syncEav = function () {
        eavPanel.classList.toggle("d-none", !eavToggle.checked);
      };
      eavToggle.addEventListener("change", syncEav);
      syncEav();
    }

    var fetchBtn = document.querySelector(".js-fetch-view-columns");
    if (fetchBtn) {
      fetchBtn.addEventListener("click", function () {
        var selectSelector = fetchBtn.getAttribute("data-target-select");
        var columnsUrl = fetchBtn.getAttribute("data-columns-url");
        var viewSelect = document.querySelector(selectSelector);
        var previewBox = document.getElementById("viewColumnsPreview");
        var badgesWrap = document.querySelector(".js-view-columns-badges");
        var targetInput = document.querySelector(".js-column-target");

        if (!viewSelect || !columnsUrl || !viewSelect.value) {
          return;
        }

        fetch(columnsUrl + "?viewName=" + encodeURIComponent(viewSelect.value))
          .then(function (res) {
            if (!res.ok) throw new Error("خطا در دریافت ستون‌های نما");
            return res.json();
          })
          .then(function (columns) {
            if (!previewBox || !badgesWrap) return;
            badgesWrap.innerHTML = "";
            (columns || []).forEach(function (col) {
              var btn = document.createElement("button");
              btn.type = "button";
              btn.className = "btn btn-sm btn-outline-secondary ltr-isolate";
              btn.textContent = col.ColumnName + " (" + col.DataType + ")";
              btn.addEventListener("click", function () {
                if (targetInput) {
                  targetInput.value = col.ColumnName;
                }
              });
              badgesWrap.appendChild(btn);
            });
            previewBox.classList.remove("d-none");
          })
          .catch(function (err) {
            alert(err.message || "خطا در دریافت اطلاعات نما");
          });
      });
    }
  }

  function initAdminCustomFields() {
    var typeSelect = document.querySelector(".js-custom-field-type");
    var datePanel = document.getElementById("cfDateOptions");
    var boolPanel = document.getElementById("cfBooleanOptions");
    var selectPanel = document.getElementById("cfSelectOptions");

    if (typeSelect) {
      var syncFieldType = function () {
        var val = String(typeSelect.value);
        if (datePanel) datePanel.classList.toggle("d-none", val !== "3");
        if (boolPanel) boolPanel.classList.toggle("d-none", val !== "4");
        if (selectPanel) selectPanel.classList.toggle("d-none", val !== "6" && val !== "7");
      };
      typeSelect.addEventListener("change", syncFieldType);
      syncFieldType();
    }

    var sourceSelect = document.querySelector(".js-options-source-type");
    var staticPanel = document.getElementById("cfStaticOptionsPanel");
    var sqlPanel = document.getElementById("cfSqlViewOptionsPanel");
    if (sourceSelect) {
      var syncSourceType = function () {
        var isSql = String(sourceSelect.value) === "2";
        if (staticPanel) staticPanel.classList.toggle("d-none", isSql);
        if (sqlPanel) sqlPanel.classList.toggle("d-none", !isSql);
      };
      sourceSelect.addEventListener("change", syncSourceType);
      syncSourceType();
    }

    var previewBtn = document.querySelector(".js-preview-options-btn");
    if (previewBtn) {
      previewBtn.addEventListener("click", function () {
        var url = previewBtn.getAttribute("data-preview-url");
        var resultBox = document.querySelector(".js-preview-options-result");
        if (!url || !resultBox) return;

        var payload = {
          OptionsSourceType: parseInt((document.getElementById("cf_OptionsSourceType") || {}).value || "1", 10),
          StaticOptionsJson: (document.getElementById("cf_StaticOptionsJson") || {}).value || "",
          OptionsViewName: (document.getElementById("cf_OptionsViewName") || {}).value || "",
          OptionsValueColumn: (document.getElementById("cf_OptionsValueColumn") || {}).value || "",
          OptionsDisplayColumn: (document.getElementById("cf_OptionsDisplayColumn") || {}).value || ""
        };

        fetch(url, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(payload)
        })
          .then(function (res) {
            if (!res.ok) return res.text().then(function (t) { throw new Error(t); });
            return res.json();
          })
          .then(function (items) {
            if (!items || items.length === 0) {
              resultBox.innerHTML = '<span class="text-warning">گزینه‌ای یافت نشد.</span>';
              return;
            }
            var preview = items.slice(0, 8).map(function (i) { return i.Text; }).join("، ");
            resultBox.innerHTML = '<span class="text-success">تعداد: ' + items.length + " | نمونه: " + preview + "</span>";
          })
          .catch(function (err) {
            resultBox.innerHTML = '<span class="text-danger">خطا: ' + (err.message || "نامعتبر") + "</span>";
          });
      });
    }
  }

  function initPrintQrSheet() {
    var printBtn = document.querySelector(".js-print-sheet-btn");
    if (printBtn) {
      printBtn.addEventListener("click", function () {
        window.print();
      });
    }

    var a4Select = document.querySelector(".js-a4-grid-select");
    var grid = document.getElementById("qrLabelGrid");
    if (a4Select && grid) {
      a4Select.addEventListener("change", function () {
        grid.classList.remove("qr-label-grid--3x6", "qr-label-grid--3x7", "qr-label-grid--3x8");
        grid.classList.add(a4Select.value);
      });
    }

    var thermalSelect = document.querySelector(".js-thermal-size-select");
    var thermalWrapper = document.getElementById("thermalPrintWrapper");
    if (thermalSelect && thermalWrapper) {
      thermalSelect.addEventListener("change", function () {
        var is60 = thermalSelect.value === "60x40";
        thermalWrapper.classList.toggle("print-mode-thermal-50x30", !is60);
        thermalWrapper.classList.toggle("print-mode-thermal-60x40", is60);
        document.querySelectorAll(".js-thermal-label").forEach(function (lbl) {
          lbl.classList.toggle("thermal-label--50x30", !is60);
          lbl.classList.toggle("thermal-label--60x40", is60);
        });
      });
    }
  }

  document.addEventListener("DOMContentLoaded", function () {
    initSelect2();
    initJalaliDatepicker();
    initFileDropzones();
    initIndicatorLookup();
    initAdminCreateDataSource();
    initAdminCustomFields();
    initPrintQrSheet();
  });
})();
